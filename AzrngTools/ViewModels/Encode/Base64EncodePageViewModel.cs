#nullable disable
using System.Text;
using Avalonia.Controls;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// Base64 文本处理
/// </summary>
public partial class Base64EncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public Base64EncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [ObservableProperty]
    private string _original;

    [ObservableProperty]
    private string _handleText;

    public int OriginalCharacterCount => Original?.Length ?? 0;

    public int OriginalByteCount => Original.IsNullOrEmpty() ? 0 : Encoding.UTF8.GetByteCount(Original);

    public int ResultCharacterCount => HandleText?.Length ?? 0;

    public int ResultByteCount => HandleText.IsNullOrEmpty() ? 0 : Encoding.UTF8.GetByteCount(HandleText);

    public bool HasInput => !Original.IsNullOrWhiteSpace();

    public bool HasResult => !HandleText.IsNullOrWhiteSpace();

    [RelayCommand]
    private void Base64Handler(string encoding)
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要编码或者解码的内容");
                return;
            }

            if (encoding.Equals("true", StringComparison.CurrentCultureIgnoreCase))
            {
                var bytes = Encoding.UTF8.GetBytes(Original);
                HandleText = Convert.ToBase64String(bytes);
            }
            else
            {
                var bytes = Convert.FromBase64String(Original);
                HandleText = Encoding.UTF8.GetString(bytes);
            }
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Original = string.Empty;
        HandleText = string.Empty;
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (HandleText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("当前没有可复制的处理结果");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, HandleText);
            }

            _messageService.SendMessage("处理结果已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制结果失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        try
        {
            var topLevel = GetTopLevel();
            var clipboardText = await ClipboardHelper.GetTextAsync(topLevel);
            if (clipboardText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("剪贴板中没有可导入的文本内容");
                return;
            }

            Original = clipboardText;
            _messageService.SendMessage($"已从剪贴板导入 {OriginalCharacterCount} 个字符");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"粘贴失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void UseResultAsInput()
    {
        if (HandleText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("当前没有可回填的结果内容");
            return;
        }

        Original = HandleText;
        _messageService.SendMessage("已将处理结果回填到输入区");
    }

    partial void OnOriginalChanged(string value)
    {
        OnPropertyChanged(nameof(OriginalCharacterCount));
        OnPropertyChanged(nameof(OriginalByteCount));
        OnPropertyChanged(nameof(HasInput));
    }

    partial void OnHandleTextChanged(string value)
    {
        OnPropertyChanged(nameof(ResultCharacterCount));
        OnPropertyChanged(nameof(ResultByteCount));
        OnPropertyChanged(nameof(HasResult));
    }

    private TopLevel? GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
