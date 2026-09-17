using System.Text.Json;
using System.Text.Json.Serialization;
using AzrngTools.Models;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AzrngTools.Services;

public sealed partial class AppUpdateCoordinatorService : IAppUpdateCoordinatorService, ISingletonDependency
{
    // 自动下载失败重试上限；全部失败后本次会话不再自动尝试，用户仍可在关于页手动下载
    private const int AutoDownloadMaxAttempts = 3;

    private const int AutoDownloadRetryDelaySeconds = 3;

    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly IAppUpdateService _appUpdateService;
    private readonly IMessageService _messageService;
    private readonly IApplicationRuntimeService _applicationRuntimeService;
    private readonly ILogger<AppUpdateCoordinatorService> _logger;
    private readonly string _cacheFilePath;
    private readonly TimeSpan _startupCheckDelay;
    private readonly TimeSpan _autoDownloadRetryDelay;
    private readonly TimeSpan _applyShutdownDelay;

    private AppUpdateInfo? _latestUpdateInfo;
    private AppUpdatePreparedPackage? _preparedPackage;
    private string? _failedDownloadVersion;
    private Task? _startupCheckTask;
    private bool _applyInitiated;

    public AppUpdateCoordinatorService(IAppUpdateService appUpdateService,
                                       IMessageService messageService,
                                       IApplicationRuntimeService applicationRuntimeService,
                                       ILogger<AppUpdateCoordinatorService> logger)
        : this(appUpdateService, messageService, applicationRuntimeService, logger,
               GetCacheFilePath(), TimeSpan.FromSeconds(3),
               TimeSpan.FromSeconds(AutoDownloadRetryDelaySeconds), TimeSpan.FromMilliseconds(800))
    {
    }

    // 测试专用：注入缓存路径与各段等待时长，避免读写真实 AppData、避免用例跑满真实的 3 秒/重试间隔
    internal AppUpdateCoordinatorService(IAppUpdateService appUpdateService,
                                         IMessageService messageService,
                                         IApplicationRuntimeService applicationRuntimeService,
                                         ILogger<AppUpdateCoordinatorService> logger,
                                         string cacheFilePath,
                                         TimeSpan startupCheckDelay,
                                         TimeSpan autoDownloadRetryDelay,
                                         TimeSpan applyShutdownDelay)
    {
        _appUpdateService = appUpdateService;
        _messageService = messageService;
        _applicationRuntimeService = applicationRuntimeService;
        _logger = logger;
        _cacheFilePath = cacheFilePath;
        _startupCheckDelay = startupCheckDelay;
        _autoDownloadRetryDelay = autoDownloadRetryDelay;
        _applyShutdownDelay = applyShutdownDelay;
        _preparedPackage = LoadPreparedPackage();

        LatestVersion = _preparedPackage?.Version ?? "未检查";
        ReleasePublishedAt = "未检查";
        UpdateStatus = _preparedPackage is null
            ? "应用启动后会自动检查更新。"
            : $"检测到已缓存版本 {_preparedPackage.Version} 的更新包，启动后会自动确认最新发布。";
        PrimaryActionText = "检查更新";
    }

    public string LatestVersion { get; private set; }

    public string ReleasePublishedAt { get; private set; }

    public string UpdateStatus { get; private set; }

    public string PrimaryActionText { get; private set; }

    public bool IsBusy { get; private set; }

    public bool CanExecutePrimaryAction => !IsBusy;

    public Task EnsureStartupCheckAsync(CancellationToken cancellationToken = default)
    {
        _startupCheckTask ??= RunStartupCheckAsync(cancellationToken);
        return _startupCheckTask;
    }

    public async Task ExecutePrimaryActionAsync(CancellationToken cancellationToken = default)
    {
        switch (GetPrimaryActionKind())
        {
            case PrimaryActionKind.Download:
            case PrimaryActionKind.RetryDownload:
                await DownloadPreparedUpdateAsync(cancellationToken);
                break;
            case PrimaryActionKind.Apply:
                await ApplyPreparedUpdateAsync(cancellationToken);
                break;
            default:
                await CheckForUpdatesCoreAsync(isAutomatic: false, notifyOnFailure: true, cancellationToken);
                break;
        }
    }

    private async Task RunStartupCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_startupCheckDelay, cancellationToken);
            await CheckForUpdatesCoreAsync(isAutomatic: true, notifyOnFailure: false, cancellationToken);
            await AutoDownloadLatestAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "启动后的自动检查更新任务发生未处理异常。");
        }
    }

    /// <summary>
    /// 自动检查发现新版本后自动下载更新包，失败最多重试 3 次；全部失败后静默放弃本轮，更新包已就绪时只提示不重复下载。
    /// </summary>
    private async Task AutoDownloadLatestAsync(CancellationToken cancellationToken)
    {
        var updateInfo = _latestUpdateInfo;
        if (updateInfo is null || !updateInfo.HasUpdate)
        {
            return;
        }

        // 启动时从本地缓存恢复的更新包与最新版本一致，直接提示就绪
        if (GetPrimaryActionKind() == PrimaryActionKind.Apply)
        {
            UpdateStatus = $"更新包已就绪，重启即更新到 {updateInfo.LatestVersion}。";
            _messageService.SendMessage("更新包已就绪，重启即更新", "发现新版本");
            PublishStateChanged();
            return;
        }

        _messageService.SendMessage($"发现新版本 {updateInfo.LatestVersion}，正在后台自动下载更新包。", "发现新版本");

        for (var attempt = 1; attempt <= AutoDownloadMaxAttempts; attempt++)
        {
            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                await DownloadCoreAsync(
                    $"发现新版本 {updateInfo.LatestVersion}，正在后台自动下载（第 {attempt}/{AutoDownloadMaxAttempts} 次）...",
                    cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _failedDownloadVersion = updateInfo.LatestVersion;
                ClearPreparedPackage(deleteFiles: true);
                _logger.LogWarning(ex, "自动下载更新包失败（第 {Attempt} 次尝试）。", attempt);

                // 静默重试直到用尽次数，仅在最终失败时提示一次
                if (attempt >= AutoDownloadMaxAttempts)
                {
                    UpdateStatus = "自动下载更新包失败，本次启动不再自动尝试，可稍后在关于页手动下载。";
                    _messageService.SendMessage("自动下载更新包失败，可前往关于页手动下载。", "更新失败");
                    PublishStateChanged();
                    return;
                }
            }
            finally
            {
                _operationLock.Release();
            }

            await Task.Delay(_autoDownloadRetryDelay, cancellationToken);
        }
    }

    private async Task CheckForUpdatesCoreAsync(bool isAutomatic,
                                                bool notifyOnFailure,
                                                CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            SetBusyState(true, isAutomatic ? "正在后台检查 GitHub 最新发布..." : "正在检查 GitHub 最新发布...");

            var updateInfo = await _appUpdateService.CheckForUpdateAsync(cancellationToken);
            _latestUpdateInfo = updateInfo;
            LatestVersion = updateInfo.LatestVersion;
            ReleasePublishedAt = updateInfo.PublishedAt == DateTimeOffset.MinValue
                ? "未知"
                : updateInfo.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            if (!updateInfo.HasUpdate)
            {
                _failedDownloadVersion = null;
                ClearPreparedPackage(deleteFiles: true);
                UpdateStatus = "当前已经是最新版本。";
                return;
            }

            if (IsPreparedPackageUsable(_preparedPackage)
                && string.Equals(_preparedPackage!.Version, updateInfo.LatestVersion, StringComparison.Ordinal))
            {
                _failedDownloadVersion = null;
                UpdateStatus = $"已下载新版本 {updateInfo.LatestVersion} 的更新包，可以立即更新。";

                // 就绪提示由随后的 AutoDownloadLatestAsync 统一发出，这里不再重复通知
                return;
            }

            if (IsPreparedPackageUsable(_preparedPackage)
                && !string.Equals(_preparedPackage!.Version, updateInfo.LatestVersion, StringComparison.Ordinal))
            {
                ClearPreparedPackage(deleteFiles: true);
            }

            UpdateStatus = $"发现新版本 {updateInfo.LatestVersion}，可以先后台下载更新包。";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UpdateStatus = isAutomatic
                ? "启动时自动检查失败，可稍后在关于页手动重试。"
                : $"检查更新失败：{ex.Message}";

            if (notifyOnFailure)
            {
                _messageService.SendMessage(UpdateStatus, "更新失败");
            }

            _logger.LogWarning(ex, "检查更新失败。");
        }
        finally
        {
            SetBusyState(false);
            _operationLock.Release();
        }
    }

    private async Task DownloadPreparedUpdateAsync(CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (_latestUpdateInfo is null || !_latestUpdateInfo.HasUpdate)
            {
                UpdateStatus = "请先检查更新，确认有新版本后再下载更新包。";
                PublishStateChanged();
                return;
            }

            try
            {
                await DownloadCoreAsync($"正在后台下载 {_latestUpdateInfo.AssetName}...", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _failedDownloadVersion = _latestUpdateInfo.LatestVersion;
                ClearPreparedPackage(deleteFiles: true);
                UpdateStatus = $"下载更新失败：{ex.Message}";
                _messageService.SendMessage(UpdateStatus, "更新失败");
                _logger.LogWarning(ex, "下载更新包失败。");
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// 下载并缓存更新包的核心流程，调用方必须已持有 <see cref="_operationLock"/> 并确认存在待下载的新版本；
    /// 失败时向上抛出，由手动/自动两条路径按各自策略处理提示。
    /// </summary>
    private async Task DownloadCoreAsync(string busyStatus, CancellationToken cancellationToken)
    {
        SetBusyState(true, busyStatus);
        try
        {
            var package = await _appUpdateService.DownloadUpdatePackageAsync(_latestUpdateInfo!, cancellationToken);
            _preparedPackage = package;
            _failedDownloadVersion = null;
            SavePreparedPackage(package);
            UpdateStatus = $"更新包已下载完成，重启即更新到 {package.Version}。";
            _messageService.SendMessage("更新包已就绪，重启即更新", "下载完成");
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task ApplyPreparedUpdateAsync(CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsPreparedPackageUsable(_preparedPackage))
            {
                ClearPreparedPackage(deleteFiles: true);
                UpdateStatus = "未找到已下载的更新包，请先重新下载。";
                PublishStateChanged();
                return;
            }

            SetBusyState(true, $"正在准备应用 {_preparedPackage!.Version} 更新...");

            var result = await _appUpdateService.ApplyPreparedUpdateAsync(_preparedPackage, restartAfterUpdate: true, cancellationToken);
            UpdateStatus = result.Message;

            if (!result.IsSuccess)
            {
                _messageService.SendMessage(result.Message, "更新失败");
                return;
            }

            // 已发起替换脚本，退出钩子不再重复发起
            _applyInitiated = true;
            _messageService.SendMessage("应用即将关闭并自动完成更新。", "开始更新");
            PublishStateChanged();
            await Task.Delay(_applyShutdownDelay, cancellationToken);
            _applicationRuntimeService.Shutdown();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UpdateStatus = $"自动更新失败：{ex.Message}";
            _messageService.SendMessage(UpdateStatus, "更新失败");
            _logger.LogWarning(ex, "应用更新失败。");
        }
        finally
        {
            SetBusyState(false);
            _operationLock.Release();
        }
    }

    public void TryApplyPreparedUpdateOnExit()
    {
        try
        {
            if (_applyInitiated || !IsPreparedPackageUsable(_preparedPackage))
            {
                return;
            }

            _applyInitiated = true;
            var package = _preparedPackage!;
            var result = _appUpdateService
                .ApplyPreparedUpdateAsync(package, restartAfterUpdate: false)
                .GetAwaiter()
                .GetResult();

            _logger.LogInformation("退出时自动应用更新包 {Version}：{Message}", package.Version, result.Message);
        }
        catch (Exception ex)
        {
            // 退出路径不能抛异常，失败时保留缓存包，下次启动检查后仍会重新尝试
            _logger.LogWarning(ex, "退出时自动应用更新包失败。");
        }
    }

    private void SetBusyState(bool isBusy, string? status = null)
    {
        IsBusy = isBusy;
        if (!string.IsNullOrWhiteSpace(status))
        {
            UpdateStatus = status;
        }

        RefreshPrimaryActionText();
        PublishStateChanged();
    }

    private void RefreshPrimaryActionText()
    {
        PrimaryActionText = GetPrimaryActionKind() switch
        {
            PrimaryActionKind.Download => "后台下载更新包",
            PrimaryActionKind.RetryDownload => "重试下载",
            PrimaryActionKind.Apply => "立即更新",
            _ => "检查更新"
        };
    }

    private PrimaryActionKind GetPrimaryActionKind()
    {
        if (_latestUpdateInfo is null || !_latestUpdateInfo.HasUpdate)
        {
            return PrimaryActionKind.Check;
        }

        if (IsPreparedPackageUsable(_preparedPackage)
            && string.Equals(_preparedPackage!.Version, _latestUpdateInfo.LatestVersion, StringComparison.Ordinal))
        {
            return PrimaryActionKind.Apply;
        }

        if (string.Equals(_failedDownloadVersion, _latestUpdateInfo.LatestVersion, StringComparison.Ordinal))
        {
            return PrimaryActionKind.RetryDownload;
        }

        return PrimaryActionKind.Download;
    }

    private AppUpdatePreparedPackage? LoadPreparedPackage()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_cacheFilePath);
            var package = JsonSerializer.Deserialize(json, AppUpdateCacheJsonContext.Default.AppUpdatePreparedPackage);
            if (IsPreparedPackageUsable(package))
            {
                return package;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取本地更新缓存失败。");
        }

        DeleteCacheFile();
        return null;
    }

    private void SavePreparedPackage(AppUpdatePreparedPackage package)
    {
        try
        {
            var directory = Path.GetDirectoryName(_cacheFilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(package, AppUpdateCacheJsonContext.Default.AppUpdatePreparedPackage);
            var tempFilePath = _cacheFilePath + ".tmp";
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _cacheFilePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入本地更新缓存失败。");
        }
    }

    private void ClearPreparedPackage(bool deleteFiles)
    {
        var package = _preparedPackage;
        _preparedPackage = null;

        if (deleteFiles && package is not null && !string.IsNullOrWhiteSpace(package.PackageRoot))
        {
            TryDeleteDirectory(package.PackageRoot);
        }

        DeleteCacheFile();
    }

    private void DeleteCacheFile()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除本地更新缓存失败。");
        }
    }

    private void PublishStateChanged()
    {
        RefreshPrimaryActionText();
        WeakReferenceMessenger.Default.Send(new AppUpdateStateChangedMessage());
    }

    private static bool IsPreparedPackageUsable(AppUpdatePreparedPackage? package)
    {
        return package is not null
               && !string.IsNullOrWhiteSpace(package.Version)
               && !string.IsNullOrWhiteSpace(package.PackageRoot)
               && !string.IsNullOrWhiteSpace(package.PayloadDirectory)
               && Directory.Exists(package.PayloadDirectory);
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
        }
    }

    private static string GetCacheFilePath()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools");
        return Path.Combine(appDataDirectory, "app-update-cache.json");
    }

    private enum PrimaryActionKind
    {
        Check,
        Download,
        RetryDownload,
        Apply
    }

    // 源生成 JSON 上下文：AOT 发布下不能用反射序列化
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppUpdatePreparedPackage))]
    private sealed partial class AppUpdateCacheJsonContext : JsonSerializerContext
    {
    }
}
