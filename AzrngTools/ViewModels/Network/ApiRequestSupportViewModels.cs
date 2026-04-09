using AzrngTools.Models.Network;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.Network;

public partial class ApiRequestParameterItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}

public partial class ApiRequestBodyModeOptionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _mode = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;
}

public partial class ApiRequestHistoryItemViewModel : ViewModelBase
{
    public required string Id { get; init; }
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required DateTime Timestamp { get; init; }
    public required ApiRequestSnapshot Request { get; init; }
    public ApiResponseSnapshot? Response { get; init; }

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _sizeText = string.Empty;
}
