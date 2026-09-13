#nullable disable
using Avalonia.Controls;
using Avalonia.Threading;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// JWT 解码器
/// </summary>
public partial class JwtEncodePageViewModel : ViewModelBase
{
    // 输入防抖间隔：避免每个按键都触发一次 Base64+JSON 全量解析
    private static readonly TimeSpan ParseDebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IMessageService _messageService;
    private readonly DebouncedActionDispatcher _parseDispatcher;

    public JwtEncodePageViewModel(IMessageService messageService, TimeSpan? parseDebounceDelay = null)
    {
        _messageService = messageService;
        _parseDispatcher = new DebouncedActionDispatcher(parseDebounceDelay ?? ParseDebounceDelay);
    }

    [ObservableProperty]
    private string _original;

    [ObservableProperty]
    private string _headerText;

    [ObservableProperty]
    private string _payloadText;

    public int JwtCharacterCount => Original?.Length ?? 0;

    public int HeaderCharacterCount => HeaderText?.Length ?? 0;

    public int PayloadCharacterCount => PayloadText?.Length ?? 0;

    public bool HasJwtInput => !Original.IsNullOrWhiteSpace();

    public bool HasHeader => !HeaderText.IsNullOrWhiteSpace();

    public bool HasPayload => !PayloadText.IsNullOrWhiteSpace();

    partial void OnOriginalChanged(string value)
    {
        OnPropertyChanged(nameof(JwtCharacterCount));
        OnPropertyChanged(nameof(HasJwtInput));

        // 清空输入是廉价操作，立即执行给出即时反馈；其余输入防抖后统一解析，
        // 避免逐键解析卡顿并重复弹通知。解析读取的是最新 Original，停顿期间不存在过期结果
        if (value.IsNullOrWhiteSpace())
        {
            HeaderText = string.Empty;
            PayloadText = string.Empty;
            return;
        }

        _parseDispatcher.Debounce(ParseOriginalInput);
    }

    /// <summary>
    /// 解码当前输入的 JWT 并填充 Header / Payload 展示内容，失败时清空结果并提示。
    /// 由防抖器在后台线程触发；属性变更通知由 Avalonia 绑定自动封送回 UI 线程。
    /// </summary>
    private void ParseOriginalInput()
    {
        var value = Original;

        try
        {
            if (value.IsNullOrWhiteSpace())
            {
                HeaderText = string.Empty;
                PayloadText = string.Empty;
                return;
            }

            if (!value.Contains("."))
            {
                HeaderText = string.Empty;
                PayloadText = string.Empty;
                NotifyParseWarning("JWT 格式有误");
                return;
            }

            value = value.Trim();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                value = value["Bearer ".Length..];
            }

            var texts = value.Split('.');
            if (texts.Length < 2)
            {
                HeaderText = string.Empty;
                PayloadText = string.Empty;
                NotifyParseWarning("JWT 格式有误");
                return;
            }

            var header = NormalizeBase64Segment(texts[0]).FromBase64Decode();
            var payload = NormalizeBase64Segment(texts[1]).FromBase64Decode();
            HeaderText = JsonHelper.JsonFormatter(header);
            PayloadText = JsonHelper.JsonFormatter(payload);
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"JWT解析失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            HeaderText = string.Empty;
            PayloadText = string.Empty;
            NotifyParseWarning($"解析失败：{ex.Message}");
        }
    }

    private void NotifyParseWarning(string message)
    {
        // 解析在后台线程执行，toast 通知必须回到 UI 线程展示
        Dispatcher.UIThread.Post(() => _messageService.SendMessage(message));
    }

    partial void OnHeaderTextChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderCharacterCount));
        OnPropertyChanged(nameof(HasHeader));
    }

    partial void OnPayloadTextChanged(string value)
    {
        OnPropertyChanged(nameof(PayloadCharacterCount));
        OnPropertyChanged(nameof(HasPayload));
    }

    [RelayCommand]
    private void Clear()
    {
        Original = string.Empty;
        HeaderText = string.Empty;
        PayloadText = string.Empty;
    }

    [RelayCommand]
    private async Task CopyHeader()
    {
        try
        {
            if (HeaderText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("当前没有可复制的 Header 内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, HeaderText);
            }

            _messageService.SendMessage("Header 已复制到剪贴板");
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"复制Header失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"复制 Header 失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyPayload()
    {
        try
        {
            if (PayloadText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("当前没有可复制的 Payload 内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, PayloadText);
            }

            _messageService.SendMessage("Payload 已复制到剪贴板");
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"复制Payload失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"复制 Payload 失败：{ex.Message}");
        }
    }

    private static string NormalizeBase64Segment(string text)
    {
        text = text.TrimEnd('=').Replace('-', '+').Replace('_', '/').Replace(" ", "+");
        if (text.Length % 4 > 0)
        {
            text = text.PadRight(text.Length + 4 - text.Length % 4, '=');
        }

        return text;
    }

    private TopLevel? GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
