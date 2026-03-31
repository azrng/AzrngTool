using AzrngTools.Services;

namespace AzrngTools.ViewModels.Setting;

public partial class AboutPageViewModel : ViewModelBase
{
    public AboutPageViewModel(IAppInfoService appInfoService)
    {
        Version = appInfoService.Version;
    }

    public string Version { get; }
}
