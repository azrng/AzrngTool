using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzrngTools.Models;

namespace AzrngTools.Services;

public sealed class AppUpdateService : IAppUpdateService, ISingletonDependency
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IAppInfoService _appInfoService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AppUpdateService(IHttpClientFactory httpClientFactory, IAppInfoService appInfoService)
    {
        _httpClientFactory = httpClientFactory;
        _appInfoService = appInfoService;
    }

    public async Task<AppUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var requestUri = $"https://api.github.com/repos/{_appInfoService.RepositoryOwner}/{_appInfoService.RepositoryName}/releases/latest";

        using var response = await client.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("当前仓库还没有正式发布的 GitHub Release，暂时无法检查更新。");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GithubReleaseResponse>(stream, SerializerOptions, cancellationToken);

        if (release is null)
        {
            throw new InvalidOperationException("未能解析 GitHub 发布信息。");
        }

        var asset = release.Assets.FirstOrDefault(item =>
                        string.Equals(item.Name, _appInfoService.UpdateAssetName, StringComparison.OrdinalIgnoreCase))
                    ?? release.Assets.FirstOrDefault(item =>
                        item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            throw new InvalidOperationException("最新发布中未找到可下载的 zip 更新包。");
        }

        var currentVersion = NormalizeVersion(_appInfoService.Version);
        var latestVersion = NormalizeVersion(release.TagName);

        return new AppUpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            HasUpdate = CompareVersions(latestVersion, currentVersion) > 0,
            DownloadUrl = asset.DownloadUrl,
            ReleasePageUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? _appInfoService.RepositoryUrl
                : release.HtmlUrl,
            AssetName = asset.Name,
            ReleaseNotes = string.IsNullOrWhiteSpace(release.Body)
                ? "该版本未提供更新说明。"
                : release.Body.Trim(),
            PublishedAt = release.PublishedAt ?? release.CreatedAt ?? DateTimeOffset.MinValue
        };
    }

    public async Task<AppUpdateApplyResult> DownloadAndPrepareUpdateAsync(AppUpdateInfo updateInfo,
                                                                          CancellationToken cancellationToken = default)
    {
        if (updateInfo is null)
        {
            throw new ArgumentNullException(nameof(updateInfo));
        }

        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            return new AppUpdateApplyResult
            {
                IsSuccess = false,
                Message = "当前版本缺少可用的更新包下载地址。"
            };
        }

        var updateRoot = Path.Combine(Path.GetTempPath(), "AzrngTools", "updates",
            $"{updateInfo.LatestVersion}-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(updateRoot, updateInfo.AssetName);
        var extractDirectory = Path.Combine(updateRoot, "payload");

        Directory.CreateDirectory(updateRoot);
        Directory.CreateDirectory(extractDirectory);

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var targetStream = File.Create(archivePath))
            {
                await response.Content.CopyToAsync(targetStream, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, extractDirectory);

            var payloadDirectory = ResolvePayloadDirectory(extractDirectory);
            var updaterScriptPath = Path.Combine(updateRoot, "apply-update.ps1");
            await File.WriteAllTextAsync(updaterScriptPath, BuildUpdateScript(payloadDirectory), Encoding.UTF8,
                cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterScriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = updateRoot
            };

            Process.Start(startInfo);

            return new AppUpdateApplyResult
            {
                IsSuccess = true,
                Message = "更新包已准备完成，应用关闭后会自动替换并重新启动。"
            };
        }
        catch
        {
            TryDeleteDirectory(updateRoot);
            throw;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(AppUpdateService));
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AzrngTools", NormalizeVersion(_appInfoService.Version)));
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private string BuildUpdateScript(string sourceDirectory)
    {
        var processId = Environment.ProcessId;
        var executablePath = EscapePowerShellString(_appInfoService.ExecutablePath);
        var targetDirectory = EscapePowerShellString(_appInfoService.BaseDirectory);
        var source = EscapePowerShellString(sourceDirectory);

        return $$"""
$ErrorActionPreference = 'Stop'
$processId = {{processId}}
$sourceDirectory = '{{source}}'
$targetDirectory = '{{targetDirectory}}'
$executablePath = '{{executablePath}}'

for ($attempt = 0; $attempt -lt 120; $attempt++) {
    $runningProcess = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if (-not $runningProcess) {
        break
    }

    Start-Sleep -Milliseconds 500
}

$copied = $false
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    try {
        Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $targetDirectory -Recurse -Force
        $copied = $true
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $copied) {
    throw '更新文件复制失败，请确认当前安装目录具备写入权限。'
}

Start-Sleep -Milliseconds 500
Start-Process -FilePath $executablePath -WorkingDirectory $targetDirectory
""";
    }

    private static string ResolvePayloadDirectory(string extractDirectory)
    {
        var subDirectories = Directory.GetDirectories(extractDirectory);
        var files = Directory.GetFiles(extractDirectory);

        if (subDirectories.Length == 1 && files.Length == 0)
        {
            return subDirectories[0];
        }

        return extractDirectory;
    }

    private static int CompareVersions(string left, string right)
    {
        var leftVersion = ParseVersion(left);
        var rightVersion = ParseVersion(right);
        return leftVersion.CompareTo(rightVersion);
    }

    private static Version ParseVersion(string value)
    {
        return Version.TryParse(NormalizeVersion(value), out var version)
            ? version
            : new Version(0, 0);
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0";
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    private static string EscapePowerShellString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
        catch
        {
            // Ignore cleanup failures for temporary files.
        }
    }

    private sealed class GithubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GithubReleaseAssetResponse> Assets { get; set; } = [];
    }

    private sealed class GithubReleaseAssetResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
