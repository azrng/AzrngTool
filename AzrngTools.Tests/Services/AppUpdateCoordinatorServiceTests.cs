using System.Text.Json;
using AzrngTools.Models;
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using AzrngTools.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzrngTools.Tests.Services;

public class AppUpdateCoordinatorServiceTests
{
    private static AppUpdateInfo CreateUpdateInfo(string latestVersion, bool hasUpdate = true)
    {
        return new AppUpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = latestVersion,
            HasUpdate = hasUpdate,
            DownloadUrl = "https://example.com/update.zip",
            ReleasePageUrl = "https://example.com/release",
            AssetName = "update.zip",
            ReleaseNotes = "说明",
            PublishedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task StartupCheck_FindsUpdate_AutoDownloadsAndNotifiesReady()
    {
        using var harness = new CoordinatorHarness();
        harness.UpdateService.CheckResult = CreateUpdateInfo("2.0.0");
        harness.UpdateService.DownloadFunc = info => Task.FromResult(harness.CreateUsablePackage(info.LatestVersion));

        await harness.Coordinator.EnsureStartupCheckAsync();

        var download = Assert.Single(harness.UpdateService.DownloadCalls);
        Assert.Equal("2.0.0", download.LatestVersion);
        Assert.Contains(harness.Messages.Messages, item => item.Message == "发现新版本 2.0.0，正在后台自动下载更新包。");
        Assert.Contains(harness.Messages.Messages, item => item is { Message: "更新包已就绪，重启即更新", Title: "下载完成" });
        Assert.Contains("重启即更新到 2.0.0", harness.Coordinator.UpdateStatus);
    }

    [Fact]
    public async Task StartupCheck_NoUpdate_DoesNotDownload()
    {
        using var harness = new CoordinatorHarness();
        harness.UpdateService.CheckResult = CreateUpdateInfo("1.0.0", hasUpdate: false);

        await harness.Coordinator.EnsureStartupCheckAsync();

        Assert.Empty(harness.UpdateService.DownloadCalls);
        Assert.Empty(harness.Messages.Messages);
        Assert.Equal("当前已经是最新版本。", harness.Coordinator.UpdateStatus);
    }

    [Fact]
    public async Task StartupCheck_CachedReadyPackage_SkipsDownloadAndNotifiesReady()
    {
        using var harness = new CoordinatorHarness(seedVersion: "2.0.0");
        harness.UpdateService.CheckResult = CreateUpdateInfo("2.0.0");

        await harness.Coordinator.EnsureStartupCheckAsync();

        Assert.Empty(harness.UpdateService.DownloadCalls);
        Assert.Contains(harness.Messages.Messages, item => item is { Message: "更新包已就绪，重启即更新", Title: "发现新版本" });
    }

    [Fact]
    public async Task StartupCheck_DownloadAlwaysFails_RetriesThreeTimesThenNotifiesOnce()
    {
        using var harness = new CoordinatorHarness();
        harness.UpdateService.CheckResult = CreateUpdateInfo("2.0.0");
        harness.UpdateService.DownloadFunc = _ => Task.FromException<AppUpdatePreparedPackage>(new InvalidOperationException("网络错误"));

        await harness.Coordinator.EnsureStartupCheckAsync();

        Assert.Equal(3, harness.UpdateService.DownloadCalls.Count);
        Assert.Single(harness.Messages.Messages.FindAll(item => item.Message == "自动下载更新包失败，可前往关于页手动下载。"));
        Assert.Contains("本次启动不再自动尝试", harness.Coordinator.UpdateStatus);
    }

    [Fact]
    public async Task StartupCheck_DownloadFailsThenSucceeds_RecoversAndNotifiesReady()
    {
        using var harness = new CoordinatorHarness();
        harness.UpdateService.CheckResult = CreateUpdateInfo("2.0.0");
        var attempts = 0;
        harness.UpdateService.DownloadFunc = info =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("首次失败");
            }

            return Task.FromResult(harness.CreateUsablePackage(info.LatestVersion));
        };

        await harness.Coordinator.EnsureStartupCheckAsync();

        Assert.Equal(2, attempts);
        Assert.Contains(harness.Messages.Messages, item => item is { Message: "更新包已就绪，重启即更新", Title: "下载完成" });
    }

    [Fact]
    public void ExitApply_WithUsablePackage_AppliesWithoutRestartAndStaysSilent()
    {
        using var harness = new CoordinatorHarness(seedVersion: "2.0.0");

        harness.Coordinator.TryApplyPreparedUpdateOnExit();

        var apply = Assert.Single(harness.UpdateService.ApplyCalls);
        Assert.False(apply.RestartAfterUpdate);
        Assert.Empty(harness.Messages.Messages);
    }

    [Fact]
    public void ExitApply_WithoutPackage_DoesNothing()
    {
        using var harness = new CoordinatorHarness();

        harness.Coordinator.TryApplyPreparedUpdateOnExit();

        Assert.Empty(harness.UpdateService.ApplyCalls);
    }

    [Fact]
    public async Task ManualApplyThenExit_AppliesOnlyOnce()
    {
        using var harness = new CoordinatorHarness();
        harness.UpdateService.CheckResult = CreateUpdateInfo("2.0.0");
        harness.UpdateService.DownloadFunc = info => Task.FromResult(harness.CreateUsablePackage(info.LatestVersion));
        harness.UpdateService.ApplyFunc = _ => Task.FromResult(new AppUpdateApplyResult { IsSuccess = true, Message = "已准备" });

        await harness.Coordinator.ExecutePrimaryActionAsync();
        await harness.Coordinator.ExecutePrimaryActionAsync();
        await harness.Coordinator.ExecutePrimaryActionAsync();
        harness.Coordinator.TryApplyPreparedUpdateOnExit();

        var apply = Assert.Single(harness.UpdateService.ApplyCalls);
        Assert.True(apply.RestartAfterUpdate);
        Assert.Equal(1, harness.Runtime.ShutdownCalls);
    }

    private sealed class CoordinatorHarness : IDisposable
    {
        public FakeAppUpdateService UpdateService { get; } = new();
        public TestMessageService Messages { get; } = new();
        public FakeApplicationRuntimeService Runtime { get; } = new();
        public AppUpdateCoordinatorService Coordinator { get; }

        private string CacheDirectory { get; }

        public CoordinatorHarness(string? seedVersion = null)
        {
            CacheDirectory = Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(CacheDirectory);

            if (seedVersion is not null)
            {
                var package = CreateUsablePackage(seedVersion);
                File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true }));
            }

            Coordinator = new AppUpdateCoordinatorService(
                UpdateService,
                Messages,
                Runtime,
                NullLogger<AppUpdateCoordinatorService>.Instance,
                CacheFilePath,
                startupCheckDelay: TimeSpan.FromMilliseconds(10),
                autoDownloadRetryDelay: TimeSpan.FromMilliseconds(10),
                applyShutdownDelay: TimeSpan.FromMilliseconds(10));
        }

        private string CacheFilePath => Path.Combine(CacheDirectory, "app-update-cache.json");

        public AppUpdatePreparedPackage CreateUsablePackage(string version)
        {
            var payloadDirectory = Path.Combine(CacheDirectory, "payload-" + version);
            Directory.CreateDirectory(payloadDirectory);
            return new AppUpdatePreparedPackage
            {
                Version = version,
                AssetName = "update.zip",
                PackageRoot = payloadDirectory,
                PayloadDirectory = payloadDirectory,
                DownloadedAt = DateTimeOffset.UtcNow
            };
        }

        public void Dispose()
        {
            // 临时目录清理失败不影响测试结果
            try
            {
                Directory.Delete(CacheDirectory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FakeAppUpdateService : IAppUpdateService
    {
        public AppUpdateInfo? CheckResult { get; set; }

        public Func<AppUpdateInfo, Task<AppUpdatePreparedPackage>> DownloadFunc { get; set; } =
            _ => throw new InvalidOperationException("测试未设置 DownloadFunc");

        public Func<AppUpdatePreparedPackage, Task<AppUpdateApplyResult>> ApplyFunc { get; set; } =
            _ => Task.FromResult(new AppUpdateApplyResult { IsSuccess = true, Message = "已准备" });

        public List<AppUpdateInfo> DownloadCalls { get; } = [];

        public List<(AppUpdatePreparedPackage Package, bool RestartAfterUpdate)> ApplyCalls { get; } = [];

        public Task<AppUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CheckResult ?? throw new InvalidOperationException("测试未设置 CheckResult"));
        }

        public Task<AppUpdatePreparedPackage> DownloadUpdatePackageAsync(AppUpdateInfo updateInfo, CancellationToken cancellationToken = default)
        {
            DownloadCalls.Add(updateInfo);
            return DownloadFunc(updateInfo);
        }

        public Task<AppUpdateApplyResult> ApplyPreparedUpdateAsync(AppUpdatePreparedPackage preparedPackage,
                                                                   bool restartAfterUpdate = true,
                                                                   CancellationToken cancellationToken = default)
        {
            ApplyCalls.Add((preparedPackage, restartAfterUpdate));
            return ApplyFunc(preparedPackage);
        }
    }

    private sealed class FakeApplicationRuntimeService : IApplicationRuntimeService
    {
        public int ShutdownCalls { get; private set; }

        public void Shutdown()
        {
            ShutdownCalls++;
        }
    }
}
