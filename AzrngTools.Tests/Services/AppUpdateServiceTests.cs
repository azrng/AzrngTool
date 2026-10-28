using AzrngTools.Models;
using AzrngTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using Xunit;

namespace AzrngTools.Tests.Services;

public class AppUpdateServiceTests
{
    private const string ScriptSourceDirectory = @"C:\程序目录\payload v2";

    [Fact]
    public void BuildUpdateScript_RestartMode_UsesRestartFlagAndSkipsGuard()
    {
        var script = AppUpdateService.BuildUpdateScript(
            ScriptSourceDirectory, @"C:\app\AzrngTools.exe", @"C:\app", processId: 1234, restartAfterUpdate: true);

        // 两种模式共用同一段脚本文本，守卫块始终存在，由标志位在运行时决定走向
        Assert.Contains("$restartAfterUpdate = $true", script);
        Assert.DoesNotContain("$restartAfterUpdate = $false", script);
        Assert.Contains("$processId = 1234", script);
        Assert.Contains("Start-Process -FilePath $executablePath", script);
    }

    [Fact]
    public void BuildUpdateScript_NoRestartMode_ContainsRelaunchGuard()
    {
        var script = AppUpdateService.BuildUpdateScript(
            ScriptSourceDirectory, @"C:\app\AzrngTools.exe", @"C:\app", processId: 1234, restartAfterUpdate: false);

        Assert.Contains("$restartAfterUpdate = $false", script);
        // 防竞态守卫：复制前检测同名进程是否已被重新拉起，是则中止
        Assert.Contains("Get-Process -Name $appProcessName", script);
        Assert.Contains("if ($relaunched.Count -gt 0) {", script);
    }

    [Fact]
    public void BuildUpdateScript_EscapesSingleQuotesInPaths()
    {
        var script = AppUpdateService.BuildUpdateScript(
            @"C:\O'Brien\payload", @"C:\O'Brien\app\AzrngTools.exe", @"C:\O'Brien\app",
            processId: 1, restartAfterUpdate: false);

        Assert.Contains(@"'C:\O''Brien\payload'", script);
        Assert.Contains(@"'C:\O''Brien\app'", script);
    }

    [Fact]
    public async Task DownloadUpdatePackageAsync_CleansStaleStagingBeforeDownload()
    {
        var stagingParent = Path.Combine(Path.GetTempPath(), "AzrngTools", "updates", "staging");
        var staleDirectory = Path.Combine(stagingParent, "stale-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staleDirectory);
        File.WriteAllText(Path.Combine(staleDirectory, "leftover.bin"), "上次被中断的下载残留");

        try
        {
            var service = CreateService();
            var updateInfo = CreateUpdateInfo("https://127.0.0.1:1/update.zip");

            // 下载地址不可达，仅验证下载动作发生前残留 staging 目录已被清理
            await Assert.ThrowsAnyAsync<Exception>(() => service.DownloadUpdatePackageAsync(updateInfo));

            Assert.False(Directory.Exists(staleDirectory));
        }
        finally
        {
            if (Directory.Exists(staleDirectory))
            {
                Directory.Delete(staleDirectory, true);
            }
        }
    }

    [Fact]
    public void ExtractArchiveWithCancellation_ExtractsNestedEntriesIntoDestination()
    {
        var tempRoot = CreateTempDirectory();
        var archivePath = Path.Combine(tempRoot, "update.zip");
        var destination = Path.Combine(tempRoot, "payload");
        try
        {
            using (var fileStream = File.Create(archivePath))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                archive.CreateEntry("app/");
                using (var writer = new StreamWriter(archive.CreateEntry("app/AzrngTools.dll").Open()))
                {
                    writer.Write("二进制占位");
                }
                using (var writer = new StreamWriter(archive.CreateEntry("说明.txt").Open()))
                {
                    writer.Write("中文内容");
                }
            }

            AppUpdateService.ExtractArchiveWithCancellation(archivePath, destination, CancellationToken.None);

            Assert.Equal("二进制占位", File.ReadAllText(Path.Combine(destination, "app", "AzrngTools.dll")));
            Assert.Equal("中文内容", File.ReadAllText(Path.Combine(destination, "说明.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void ExtractArchiveWithCancellation_CanceledBeforeStart_ExtractsNothing()
    {
        var tempRoot = CreateTempDirectory();
        var archivePath = Path.Combine(tempRoot, "update.zip");
        var destination = Path.Combine(tempRoot, "payload");
        try
        {
            using (var fileStream = File.Create(archivePath))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("app/readme.txt").Open());
                writer.Write("内容");
            }

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                AppUpdateService.ExtractArchiveWithCancellation(archivePath, destination, cancellationTokenSource.Token));

            // 取消发生在第一个条目解压前，不应产生任何半成品文件
            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void ExtractArchiveWithCancellation_PathTraversalEntry_ThrowsAndLeavesNothing()
    {
        var tempRoot = CreateTempDirectory();
        var archivePath = Path.Combine(tempRoot, "update.zip");
        var destination = Path.Combine(tempRoot, "payload");
        try
        {
            using (var fileStream = File.Create(archivePath))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                archive.CreateEntry("../evil.txt");
            }

            Assert.Throws<IOException>(() =>
                AppUpdateService.ExtractArchiveWithCancellation(archivePath, destination, CancellationToken.None));

            // 越界条目必须被拒绝，不能写到目标目录之外
            Assert.False(File.Exists(Path.Combine(tempRoot, "evil.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static AppUpdateService CreateService()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(AppUpdateService));
        var httpClientFactory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();

        return new AppUpdateService(
            httpClientFactory,
            new FakeAppInfoService(),
            NullLogger<AppUpdateService>.Instance);
    }

    private static AppUpdateInfo CreateUpdateInfo(string downloadUrl)
    {
        return new AppUpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "2.0.0",
            HasUpdate = true,
            DownloadUrl = downloadUrl,
            ReleasePageUrl = "https://example.com/release",
            AssetName = "update.zip",
            ReleaseNotes = "说明"
        };
    }

    private sealed class FakeAppInfoService : IAppInfoService
    {
        public string Version => "1.0.0";

        public string InformationalVersion => "1.0.0";

        public string RepositoryOwner => "azrng";

        public string RepositoryName => "AzrngTool";

        public string RepositoryUrl => "https://github.com/azrng/AzrngTool";

        public string UpdateAssetName => "update.zip";

        public string BaseDirectory => AppContext.BaseDirectory;

        public string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "AzrngTools.exe");
    }
}
