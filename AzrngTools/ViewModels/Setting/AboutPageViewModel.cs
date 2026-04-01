using AzrngTools.Models;
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Setting;

public partial class AboutPageViewModel : ViewModelBase
{
    private readonly IAppInfoService _appInfoService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly IApplicationRuntimeService _applicationRuntimeService;
    private readonly IMessageService _messageService;
    private AppUpdateInfo _latestUpdateInfo;

    public AboutPageViewModel(IAppInfoService appInfoService,
                              IAppUpdateService appUpdateService,
                              IApplicationRuntimeService applicationRuntimeService,
                              IMessageService messageService)
    {
        _appInfoService = appInfoService;
        _appUpdateService = appUpdateService;
        _applicationRuntimeService = applicationRuntimeService;
        _messageService = messageService;

        Version = appInfoService.Version;
        LatestVersion = "未检查";
        ReleasePublishedAt = "未检查";
        UpdateStatus = "点击“检查更新”获取最新发布。";
    }

    public string Version { get; }

    public string RepositoryUrl => _appInfoService.RepositoryUrl;

    public bool CanCheckForUpdates => !IsBusy;

    public bool CanStartUpdate => !IsBusy && HasUpdate && _latestUpdateInfo is not null;

    [ObservableProperty]
    private string _latestVersion;

    [ObservableProperty]
    private string _releasePublishedAt;

    [ObservableProperty]
    private string _updateStatus;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnHasUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartUpdate));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckForUpdates));
        OnPropertyChanged(nameof(CanStartUpdate));
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            IsBusy = true;
            UpdateStatus = "正在检查 GitHub 最新发布...";

            _latestUpdateInfo = await _appUpdateService.CheckForUpdateAsync();
            LatestVersion = _latestUpdateInfo.LatestVersion;
            ReleasePublishedAt = _latestUpdateInfo.PublishedAt == DateTimeOffset.MinValue
                ? "未知"
                : _latestUpdateInfo.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            HasUpdate = _latestUpdateInfo.HasUpdate;
            UpdateStatus = HasUpdate
                ? $"发现新版本 {_latestUpdateInfo.LatestVersion}，可以立即更新。"
                : "当前已经是最新版本。";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"检查更新失败：{ex.Message}";
            _messageService.SendMessage(UpdateStatus, "更新失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartUpdateAsync()
    {
        if (_latestUpdateInfo is null || !HasUpdate)
        {
            _messageService.SendMessage("请先检查更新，确认有新版本后再执行自动更新。");
            return;
        }

        try
        {
            IsBusy = true;
            UpdateStatus = $"正在下载 {_latestUpdateInfo.AssetName}...";

            var result = await _appUpdateService.DownloadAndPrepareUpdateAsync(_latestUpdateInfo);
            UpdateStatus = result.Message;

            if (!result.IsSuccess)
            {
                _messageService.SendMessage(result.Message, "更新失败");
                return;
            }

            _messageService.SendMessage("应用即将关闭并自动完成更新。", "开始更新");
            await Task.Delay(800);
            _applicationRuntimeService.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatus = $"自动更新失败：{ex.Message}";
            _messageService.SendMessage(UpdateStatus, "更新失败");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
