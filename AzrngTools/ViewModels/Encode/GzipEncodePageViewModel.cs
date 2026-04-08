#nullable disable
using Avalonia.Controls;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// Gzip编码
/// </summary>
public partial class GzipEncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public GzipEncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    public bool HasResult => !ResultText.IsNullOrWhiteSpace();

    partial void OnResultTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

    [RelayCommand]
    private void Handler(string isEncoding)
    {
        try
        {
            if (OriginalText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            ResultText = isEncoding.Equals("true", StringComparison.CurrentCultureIgnoreCase)
                ? CompressHelper.Compress(OriginalText)
                : CompressHelper.Decompress(OriginalText);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"处理异常：{ex.Message}");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        OriginalText = string.Empty;
        ResultText = string.Empty;
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (ResultText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel?.Clipboard is not null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, ResultText);
            }

            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
