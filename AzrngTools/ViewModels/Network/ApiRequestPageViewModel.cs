using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AzrngTools.Models.Network;
using AzrngTools.Services.Network;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Network;

public partial class ApiRequestPageViewModel : ViewModelBase
{
    private readonly IApiRequestExecutionService _executionService;
    private readonly IApiRequestStoreService _storeService;
    private readonly IMessageService _messageService;
    private readonly object _requestLock = new();
    private CancellationTokenSource? _requestCancellationTokenSource;
    private List<ApiRequestHistoryItemViewModel> _allHistoryItems = [];

    public ApiRequestPageViewModel(
        IApiRequestExecutionService executionService,
        IApiRequestStoreService storeService,
        IMessageService messageService)
    {
        _executionService = executionService;
        _storeService = storeService;
        _messageService = messageService;

        HttpMethods =
        [
            "GET",
            "POST",
            "PUT",
            "DELETE",
            "PATCH",
            "HEAD",
            "OPTIONS"
        ];

        BodyModeOptions =
        [
            new ApiRequestBodyModeOptionViewModel { Mode = ApiRequestBodyModes.None, DisplayName = "不发送 Body" },
            new ApiRequestBodyModeOptionViewModel { Mode = ApiRequestBodyModes.RawJson, DisplayName = "JSON" },
            new ApiRequestBodyModeOptionViewModel { Mode = ApiRequestBodyModes.RawXml, DisplayName = "XML" },
            new ApiRequestBodyModeOptionViewModel { Mode = ApiRequestBodyModes.RawText, DisplayName = "Text" },
            new ApiRequestBodyModeOptionViewModel { Mode = ApiRequestBodyModes.FormUrlEncoded, DisplayName = "x-www-form-urlencoded" },
            new ApiRequestBodyModeOptionViewModel { Mode = ApiRequestBodyModes.FormData, DisplayName = "form-data" }
        ];

        SelectedBodyModeOption = BodyModeOptions[0];
        _ = InitializeAsync();
    }

    public IReadOnlyList<string> HttpMethods { get; }

    public ObservableCollection<ApiRequestParameterItemViewModel> QueryParameters { get; } = [];

    public ObservableCollection<ApiRequestParameterItemViewModel> Headers { get; } = [];

    public ObservableCollection<ApiRequestParameterItemViewModel> FormFields { get; } = [];

    public ObservableCollection<ApiRequestHistoryItemViewModel> HistoryItems { get; } = [];

    public ObservableCollection<ApiRequestBodyModeOptionViewModel> BodyModeOptions { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRequestInFlight;

    [ObservableProperty]
    private string _requestLoadingText = "正在发送请求...";

    [ObservableProperty]
    private string _statusMessage = "准备就绪。";

    [ObservableProperty]
    private string _selectedMethod = "GET";

    [ObservableProperty]
    private string _requestUrl = "https://";

    [ObservableProperty]
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private bool _ignoreSslErrors;

    [ObservableProperty]
    private int _selectedConfigTabIndex;

    [ObservableProperty]
    private int _selectedResponseTabIndex;

    [ObservableProperty]
    private string _selectedBodyMode = ApiRequestBodyModes.None;

    [ObservableProperty]
    private ApiRequestBodyModeOptionViewModel? _selectedBodyModeOption;

    [ObservableProperty]
    private string _historySearchText = string.Empty;

    [ObservableProperty]
    private bool _isHistoryPaneOpen;

    [ObservableProperty]
    private bool _hasHistoryItems;

    [ObservableProperty]
    private string _historySummaryText = "暂无历史记录。";

    [ObservableProperty]
    private bool _hasResponse;

    [ObservableProperty]
    private bool _showResponsePlaceholder = true;

    [ObservableProperty]
    private string _responseRequestUrlText = string.Empty;

    [ObservableProperty]
    private string _responseStatusText = string.Empty;

    [ObservableProperty]
    private string _responseDurationText = string.Empty;

    [ObservableProperty]
    private string _responseSizeText = string.Empty;

    [ObservableProperty]
    private string _responseBodyText = string.Empty;

    [ObservableProperty]
    private string _responseHeadersText = string.Empty;

    public bool IsFormBodyMode =>
        SelectedBodyMode == ApiRequestBodyModes.FormData || SelectedBodyMode == ApiRequestBodyModes.FormUrlEncoded;

    public bool HasRawBodyEditor =>
        SelectedBodyMode is ApiRequestBodyModes.RawJson or ApiRequestBodyModes.RawXml or ApiRequestBodyModes.RawText;

    public string HistoryPaneToggleText => IsHistoryPaneOpen ? "收起历史" : "历史侧栏";

    public bool ShowHistoryEmptyPlaceholder => !HasHistoryItems;

    public bool ShowResponsePlaceholderCard => ShowResponsePlaceholder && !IsRequestInFlight;

    public string RequestBodyPlaceholder => SelectedBodyMode switch
    {
        ApiRequestBodyModes.RawJson => "输入 JSON 请求体",
        ApiRequestBodyModes.RawXml => "输入 XML 请求体",
        ApiRequestBodyModes.RawText => "输入纯文本请求体",
        _ => "当前 Body 模式不需要原始文本。"
    };

    private async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            await RefreshHistoryAsync();
            StatusMessage = "接口工作台已就绪。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"初始化失败：{exception.Message}";
            _messageService.SendMessage(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedBodyModeOptionChanged(ApiRequestBodyModeOptionViewModel? value)
    {
        if (value is not null && SelectedBodyMode != value.Mode)
        {
            SelectedBodyMode = value.Mode;
        }
    }

    partial void OnSelectedBodyModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsFormBodyMode));
        OnPropertyChanged(nameof(HasRawBodyEditor));
        OnPropertyChanged(nameof(RequestBodyPlaceholder));

        var match = BodyModeOptions.FirstOrDefault(item => item.Mode == value);
        if (match is not null && !ReferenceEquals(match, SelectedBodyModeOption))
        {
            SelectedBodyModeOption = match;
        }
    }

    partial void OnHistorySearchTextChanged(string value)
    {
        ApplyHistoryFilter();
    }

    partial void OnIsHistoryPaneOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(HistoryPaneToggleText));
    }

    partial void OnHasHistoryItemsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowHistoryEmptyPlaceholder));
    }

    partial void OnIsRequestInFlightChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowResponsePlaceholderCard));
    }

    partial void OnShowResponsePlaceholderChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowResponsePlaceholderCard));
    }

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        if (IsRequestInFlight)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            StatusMessage = "请求地址不能为空。";
            _messageService.SendMessage(StatusMessage);
            return;
        }

        var snapshot = BuildRequestSnapshot();
        var cancellationTokenSource = new CancellationTokenSource();
        lock (_requestLock)
        {
            _requestCancellationTokenSource?.Dispose();
            _requestCancellationTokenSource = cancellationTokenSource;
        }

        try
        {
            IsRequestInFlight = true;
            RequestLoadingText = $"正在发送 {snapshot.Method} 请求...";
            StatusMessage = RequestLoadingText;

            var result = await _executionService.SendAsync(snapshot, cancellationTokenSource.Token);
            ApplyResponse(result.Response, result.IsSuccess);
            StatusMessage = result.Message;

            if (!result.IsCanceled)
            {
                await _storeService.AddHistoryAsync(snapshot, result.Response, CancellationToken.None);
                await RefreshHistoryAsync();
            }

            if (!result.IsSuccess && !result.IsCanceled)
            {
                _messageService.SendMessage(result.Message);
            }
        }
        finally
        {
            IsRequestInFlight = false;

            lock (_requestLock)
            {
                if (ReferenceEquals(_requestCancellationTokenSource, cancellationTokenSource))
                {
                    _requestCancellationTokenSource = null;
                }
            }

            cancellationTokenSource.Dispose();
        }
    }

    [RelayCommand]
    private void CancelRequest()
    {
        CancellationTokenSource? cancellationTokenSource;
        lock (_requestLock)
        {
            cancellationTokenSource = _requestCancellationTokenSource;
        }

        if (cancellationTokenSource is null || cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        RequestLoadingText = "正在取消请求...";
        StatusMessage = RequestLoadingText;
        cancellationTokenSource.Cancel();
    }

    [RelayCommand]
    private void ToggleHistoryPane()
    {
        if (IsRequestInFlight)
        {
            return;
        }

        IsHistoryPaneOpen = !IsHistoryPaneOpen;
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _storeService.ClearHistoryAsync(CancellationToken.None);
        await RefreshHistoryAsync();
        StatusMessage = "请求历史已清空。";
    }

    [RelayCommand]
    private async Task DeleteHistoryItemAsync(ApiRequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _storeService.DeleteHistoryAsync(item.Id, CancellationToken.None);
        await RefreshHistoryAsync();
        StatusMessage = "已删除所选历史记录。";
    }

    [RelayCommand]
    private void LoadHistoryItem(ApiRequestHistoryItemViewModel? item)
    {
        if (item is null || IsRequestInFlight)
        {
            return;
        }

        SelectedMethod = item.Request.Method;
        RequestUrl = item.Request.Url;
        ApplySnapshot(item.Request);
        ApplyResponse(item.Response, item.Response?.StatusCode is not null && string.IsNullOrWhiteSpace(item.Response.ErrorMessage));
        IsHistoryPaneOpen = false;
        StatusMessage = $"已载入 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的请求记录。";
    }

    [RelayCommand]
    private void AddQueryParameter()
    {
        QueryParameters.Add(new ApiRequestParameterItemViewModel());
    }

    [RelayCommand]
    private void AddHeader()
    {
        Headers.Add(new ApiRequestParameterItemViewModel());
    }

    [RelayCommand]
    private void AddFormField()
    {
        FormFields.Add(new ApiRequestParameterItemViewModel());
    }

    [RelayCommand]
    private void RemoveParameter(ApiRequestParameterItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        QueryParameters.Remove(item);
        Headers.Remove(item);
        FormFields.Remove(item);
    }

    [RelayCommand]
    private void ClearCurrentRequest()
    {
        if (IsRequestInFlight)
        {
            return;
        }

        SelectedMethod = "GET";
        RequestUrl = "https://";
        RequestBody = string.Empty;
        IgnoreSslErrors = false;
        SelectedBodyMode = ApiRequestBodyModes.None;

        QueryParameters.Clear();
        Headers.Clear();
        FormFields.Clear();

        ResetResponse();
        StatusMessage = "当前请求配置已清空。";
    }

    [RelayCommand]
    private async Task CopyResponseBodyAsync()
    {
        if (string.IsNullOrWhiteSpace(ResponseBodyText))
        {
            _messageService.SendMessage("当前没有可复制的响应体内容。");
            return;
        }

        await ClipboardHelper.SetTextAsync(GetTopLevel(), ResponseBodyText);
        _messageService.SendMessage("响应体已复制到剪贴板。");
    }

    [RelayCommand]
    private async Task CopyResponseHeadersAsync()
    {
        if (string.IsNullOrWhiteSpace(ResponseHeadersText))
        {
            _messageService.SendMessage("当前没有可复制的响应头内容。");
            return;
        }

        await ClipboardHelper.SetTextAsync(GetTopLevel(), ResponseHeadersText);
        _messageService.SendMessage("响应头已复制到剪贴板。");
    }

    private ApiRequestSnapshot BuildRequestSnapshot()
    {
        var snapshot = new ApiRequestSnapshot
        {
            Method = SelectedMethod,
            Url = RequestUrl,
            BodyMode = SelectedBodyMode,
            BodyContent = RequestBody,
            IgnoreSslErrors = IgnoreSslErrors,
            QueryParameters = QueryParameters.Select(ToKeyValue).ToList(),
            Headers = Headers.Select(ToKeyValue).ToList(),
            FormFields = FormFields.Select(ToKeyValue).ToList()
        };

        if (snapshot.BodyMode is ApiRequestBodyModes.FormData or ApiRequestBodyModes.FormUrlEncoded)
        {
            snapshot.BodyContent = SerializeFormFields(snapshot.FormFields);
        }

        return snapshot;
    }

    private void ApplySnapshot(ApiRequestSnapshot snapshot)
    {
        RequestBody = snapshot.BodyContent;
        IgnoreSslErrors = snapshot.IgnoreSslErrors;
        SelectedBodyMode = snapshot.BodyMode;

        ResetCollection(QueryParameters, snapshot.QueryParameters.Select(ToViewModel));
        ResetCollection(Headers, snapshot.Headers.Select(ToViewModel));

        var formFields = snapshot.FormFields.Count > 0
            ? snapshot.FormFields
            : ApiRequestExecutionService.ParseKeyValuePairs(snapshot.BodyContent);
        ResetCollection(FormFields, formFields.Select(ToViewModel));

        if (SelectedBodyMode is ApiRequestBodyModes.FormData or ApiRequestBodyModes.FormUrlEncoded)
        {
            RequestBody = string.Empty;
        }
    }

    private void ApplyResponse(ApiResponseSnapshot? response, bool success)
    {
        if (response is null)
        {
            ResetResponse();
            return;
        }

        HasResponse = true;
        ShowResponsePlaceholder = false;
        ResponseRequestUrlText = response.FinalUrl;
        ResponseStatusText = success && response.StatusCode is { } code
            ? $"HTTP {code}"
            : "请求失败";
        ResponseDurationText = response.DurationMs > 0 ? $"{response.DurationMs} ms" : "未返回耗时";
        ResponseSizeText = response.SizeBytes > 0 ? FormatBytes(response.SizeBytes) : "0 B";
        ResponseBodyText = response.Content;
        ResponseHeadersText = response.Headers.Count > 0
            ? string.Join(Environment.NewLine, response.Headers.Select(item => $"{item.Name}: {item.Value}"))
            : "未返回响应头。";
    }

    private void ResetResponse()
    {
        HasResponse = false;
        ShowResponsePlaceholder = true;
        ResponseRequestUrlText = string.Empty;
        ResponseStatusText = string.Empty;
        ResponseDurationText = string.Empty;
        ResponseSizeText = string.Empty;
        ResponseBodyText = string.Empty;
        ResponseHeadersText = string.Empty;
    }

    private async Task RefreshHistoryAsync()
    {
        var history = await _storeService.GetHistoryAsync(CancellationToken.None);
        _allHistoryItems = history.Select(ToHistoryItemViewModel).ToList();
        ApplyHistoryFilter();
    }

    private void ApplyHistoryFilter()
    {
        var keyword = HistorySearchText?.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allHistoryItems
            : _allHistoryItems.Where(item =>
                    item.Method.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.StatusText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        ResetCollection(HistoryItems, filtered);
        HasHistoryItems = filtered.Count > 0;

        HistorySummaryText = _allHistoryItems.Count switch
        {
            0 => "暂无历史记录。",
            _ when filtered.Count == _allHistoryItems.Count => $"共 {_allHistoryItems.Count} 条历史记录。",
            _ => $"筛选结果 {filtered.Count} / {_allHistoryItems.Count}。"
        };

        if (!HasHistoryItems)
        {
            IsHistoryPaneOpen = false;
        }
    }

    private static ApiRequestKeyValueItem ToKeyValue(ApiRequestParameterItemViewModel item)
    {
        return new ApiRequestKeyValueItem
        {
            Name = item.Name,
            Value = item.Value
        };
    }

    private static ApiRequestParameterItemViewModel ToViewModel(ApiRequestKeyValueItem item)
    {
        return new ApiRequestParameterItemViewModel
        {
            Name = item.Name,
            Value = item.Value
        };
    }

    private static ApiRequestHistoryItemViewModel ToHistoryItemViewModel(ApiRequestHistoryItem item)
    {
        var response = item.Response;
        var statusText = response?.StatusCode is { } statusCode
            ? statusCode.ToString()
            : string.IsNullOrWhiteSpace(response?.ErrorMessage)
                ? "-"
                : "失败";

        return new ApiRequestHistoryItemViewModel
        {
            Id = item.Id,
            Method = item.Request.Method,
            Url = item.Request.Url,
            Timestamp = item.Timestamp,
            Request = item.Request,
            Response = response,
            StatusText = statusText,
            DurationText = response?.DurationMs > 0 ? $"{response.DurationMs} ms" : "-",
            SizeText = response?.SizeBytes > 0 ? FormatBytes(response.SizeBytes) : "-"
        };
    }

    private static void ResetCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string SerializeFormFields(IEnumerable<ApiRequestKeyValueItem> fields)
    {
        return string.Join("&", fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => $"{Uri.EscapeDataString(item.Name)}={Uri.EscapeDataString(item.Value)}"));
    }

    private static string FormatBytes(long sizeBytes)
    {
        var units = new[] { "B", "KB", "MB", "GB" };
        double current = sizeBytes;
        var index = 0;

        while (current >= 1024 && index < units.Length - 1)
        {
            current /= 1024;
            index++;
        }

        return $"{current:0.##} {units[index]}";
    }

    private static TopLevel? GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
