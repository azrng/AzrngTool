using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AzrngTools.Models;
using Microsoft.Extensions.Logging;

namespace AzrngTools.Services;

public sealed partial class AppUpdateService : IAppUpdateService, ISingletonDependency
{
    private readonly IAppInfoService _appInfoService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(IHttpClientFactory httpClientFactory,
                            IAppInfoService appInfoService,
                            ILogger<AppUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _appInfoService = appInfoService;
        _logger = logger;
    }

    public async Task<AppUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var requestUri = $"https://api.github.com/repos/{_appInfoService.RepositoryOwner}/{_appInfoService.RepositoryName}/releases/latest";
        var release = await ExecuteWithCompatibilityRetryAsync(
            client => FetchLatestReleaseAsync(client, requestUri, cancellationToken),
            cancellationToken);

        var asset = release.Assets.FirstOrDefault(item =>
                        string.Equals(item.Name, _appInfoService.UpdateAssetName, StringComparison.OrdinalIgnoreCase))
                    ?? release.Assets.FirstOrDefault(item =>
                        item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            throw new InvalidOperationException("最新发布中未找到可下载的 zip 更新包。");
        }

        var currentVersion = ExtractComparableVersion(_appInfoService.Version);
        var latestVersion = ExtractComparableVersion(release.TagName);

        return new AppUpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            HasUpdate = HasNewerRelease(currentVersion, latestVersion),
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

    public async Task<AppUpdatePreparedPackage> DownloadUpdatePackageAsync(AppUpdateInfo updateInfo,
                                                                           CancellationToken cancellationToken = default)
    {
        if (updateInfo is null)
        {
            throw new ArgumentNullException(nameof(updateInfo));
        }

        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            throw new InvalidOperationException("当前版本缺少可用的更新包下载地址。");
        }

        var preparedRoot = Path.Combine(Path.GetTempPath(), "AzrngTools", "updates", "prepared", updateInfo.LatestVersion);
        var stagingRoot = Path.Combine(Path.GetTempPath(), "AzrngTools", "updates", "staging",
            $"{updateInfo.LatestVersion}-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(stagingRoot, updateInfo.AssetName);
        var extractDirectory = Path.Combine(stagingRoot, "payload");

        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(extractDirectory);

        try
        {
            await ExecuteWithCompatibilityRetryAsync(
                async client =>
                {
                    using var response = await client.GetAsync(updateInfo.DownloadUrl,
                        HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    await using var targetStream = File.Create(archivePath);
                    await response.Content.CopyToAsync(targetStream, cancellationToken);
                    return true;
                },
                cancellationToken);

            ZipFile.ExtractToDirectory(archivePath, extractDirectory);
            TryDeleteDirectory(preparedRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(preparedRoot)!);
            Directory.Move(stagingRoot, preparedRoot);

            var finalExtractDirectory = Path.Combine(preparedRoot, "payload");
            return new AppUpdatePreparedPackage
            {
                Version = updateInfo.LatestVersion,
                AssetName = updateInfo.AssetName,
                PackageRoot = preparedRoot,
                PayloadDirectory = ResolvePayloadDirectory(finalExtractDirectory),
                DownloadedAt = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            TryDeleteDirectory(stagingRoot);
            throw;
        }
    }

    public async Task<AppUpdateApplyResult> ApplyPreparedUpdateAsync(AppUpdatePreparedPackage preparedPackage,
                                                                     CancellationToken cancellationToken = default)
    {
        if (preparedPackage is null)
        {
            throw new ArgumentNullException(nameof(preparedPackage));
        }

        if (!Directory.Exists(preparedPackage.PayloadDirectory))
        {
            return new AppUpdateApplyResult
            {
                IsSuccess = false,
                Message = "未找到已下载的更新包，请先重新下载。"
            };
        }

        var updaterScriptPath = Path.Combine(preparedPackage.PackageRoot, "apply-update.ps1");
        await File.WriteAllTextAsync(updaterScriptPath, BuildUpdateScript(preparedPackage.PayloadDirectory), Encoding.UTF8,
            cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterScriptPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = preparedPackage.PackageRoot
        };

        Process.Start(startInfo);

        return new AppUpdateApplyResult
        {
            IsSuccess = true,
            Message = "更新包已准备完成，应用关闭后会自动替换并重新启动。"
        };
    }

    private async Task<T> ExecuteWithCompatibilityRetryAsync<T>(Func<HttpClient, Task<T>> operation,
                                                                CancellationToken cancellationToken)
    {
        using var primaryClient = CreatePrimaryClient();

        try
        {
            return await operation(primaryClient);
        }
        catch (Exception ex) when (ShouldRetryWithCompatibilityHandler(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "GitHub 更新请求首次尝试失败，开始使用兼容模式重试。");

            using var compatibilityClient = CreateCompatibilityClient();
            try
            {
                return await operation(compatibilityClient);
            }
            catch (Exception retryEx) when (IsSslRelatedException(retryEx))
            {
                _logger.LogError(retryEx, "GitHub 更新请求兼容模式重试仍然失败。");
                throw new InvalidOperationException("与 GitHub 建立安全连接失败，请检查系统时间、代理/VPN 或证书环境后重试。", retryEx);
            }
        }
    }

    private async Task<GithubReleaseResponse> FetchLatestReleaseAsync(HttpClient client,
                                                                      string requestUri,
                                                                      CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("当前仓库还没有正式发布的 GitHub Release，暂时无法检查更新。");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync(
            stream,
            AppUpdateJsonContext.Default.GithubReleaseResponse,
            cancellationToken);

        if (release is null)
        {
            throw new InvalidOperationException("未能解析 GitHub 发布信息。");
        }

        return release;
    }

    private HttpClient CreatePrimaryClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(AppUpdateService));
        ApplyDefaultHeaders(client);
        return client;
    }

    private HttpClient CreateCompatibilityClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            CheckCertificateRevocationList = false,
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        };

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        ApplyDefaultHeaders(client);
        return client;
    }

    private void ApplyDefaultHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AzrngTools",
            ExtractComparableVersion(_appInfoService.Version)));
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    private static bool ShouldRetryWithCompatibilityHandler(Exception exception, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested && IsSslRelatedException(exception);
    }

    private static bool IsSslRelatedException(Exception exception)
    {
        return exception is AuthenticationException
               || exception is IOException
               || exception is HttpRequestException
               || exception.InnerException is not null && IsSslRelatedException(exception.InnerException)
               || exception is AggregateException aggregate && aggregate.InnerExceptions.Any(IsSslRelatedException)
               || exception is not null && exception.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase);
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

    private static bool HasNewerRelease(string currentVersion, string latestVersion)
    {
        return ParseVersion(latestVersion).CompareTo(ParseVersion(currentVersion)) > 0;
    }

    private static Version ParseVersion(string value)
    {
        return Version.TryParse(ExtractComparableVersion(value), out var version)
            ? version
            : new Version(0, 0);
    }

    private static string ExtractComparableVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0";
        }

        var match = Regex.Match(value, @"\d+\.\d+\.\d+(?:\.\d+)?");
        if (match.Success)
        {
            return NormalizeVersionText(match.Value);
        }

        return NormalizeVersionText(value.Trim().TrimStart('v', 'V'));
    }

    private static string NormalizeVersionText(string value)
    {
        return Version.TryParse(value, out var version) && version.Revision == 0
            ? version.ToString(3)
            : value;
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

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(GithubReleaseResponse))]
    private sealed partial class AppUpdateJsonContext : JsonSerializerContext
    {
    }
}
