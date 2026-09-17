using AzrngTools.Models;
using AzrngTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
