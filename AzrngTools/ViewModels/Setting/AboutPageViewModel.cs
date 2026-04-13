using AzrngTools.Services;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AzrngTools.ViewModels.Setting;

public partial class AboutPageViewModel : ViewModelBase
{
    private readonly IAppUpdateCoordinatorService _appUpdateCoordinatorService;

    public AboutPageViewModel(IAppInfoService appInfoService,
                              IAppUpdateCoordinatorService appUpdateCoordinatorService)
    {
        _appUpdateCoordinatorService = appUpdateCoordinatorService;

        Version = appInfoService.Version;
        RepositoryUrl = appInfoService.RepositoryUrl;
        RefreshFromCoordinator();

        WeakReferenceMessenger.Default.Register<AppUpdateStateChangedMessage>(this, static (recipient, _) =>
        {
            ((AboutPageViewModel)recipient).RefreshFromCoordinator();
        });
    }

    public string Version { get; }

    public string RepositoryUrl { get; }

    [ObservableProperty]
    private string _latestVersion = "未检查";

    [ObservableProperty]
    private string _releasePublishedAt = "未检查";

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    [ObservableProperty]
    private string _primaryUpdateActionText = "检查更新";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canExecutePrimaryUpdateAction;

    [RelayCommand]
    private async Task PrimaryUpdateActionAsync()
    {
        await _appUpdateCoordinatorService.ExecutePrimaryActionAsync();
        RefreshFromCoordinator();
    }

    private void RefreshFromCoordinator()
    {
        LatestVersion = _appUpdateCoordinatorService.LatestVersion;
        ReleasePublishedAt = _appUpdateCoordinatorService.ReleasePublishedAt;
        UpdateStatus = _appUpdateCoordinatorService.UpdateStatus;
        PrimaryUpdateActionText = _appUpdateCoordinatorService.PrimaryActionText;
        IsBusy = _appUpdateCoordinatorService.IsBusy;
        CanExecutePrimaryUpdateAction = _appUpdateCoordinatorService.CanExecutePrimaryAction;
    }
}
