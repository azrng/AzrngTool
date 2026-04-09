#nullable disable
using Avalonia.Controls;
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
    private readonly IMessageService _messageService;

    public JwtEncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
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
                _messageService.SendMessage("JWT 格式有误");
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
                _messageService.SendMessage("JWT 格式有误");
                return;
            }

            var header = NormalizeBase64Segment(texts[0]).FromBase64Decode();
            var payload = NormalizeBase64Segment(texts[1]).FromBase64Decode();
            HeaderText = JsonHelper.JsonFormatter(header);
            PayloadText = JsonHelper.JsonFormatter(payload);
        }
        catch (Exception ex)
        {
            HeaderText = string.Empty;
            PayloadText = string.Empty;
            _messageService.SendMessage($"解析失败：{ex.Message}");
        }
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
