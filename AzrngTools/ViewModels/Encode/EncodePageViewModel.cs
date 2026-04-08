#nullable disable
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// url编码
/// </summary>
public partial class EncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public EncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// 原始文本
    /// </summary>
    [ObservableProperty]
    private string _originalText;

    /// <summary>
    /// 结束文本
    /// </summary>
    [ObservableProperty]
    private string _resultText;

    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="isEncoding"></param>
    [RelayCommand]
    private void UrlHandler(string isEncoding)
    {
        try
        {
            if (OriginalText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            ResultText = isEncoding.Equals("true", StringComparison.CurrentCultureIgnoreCase)
                ? OriginalText.UrlEncode()
                : OriginalText.UrlDecode();
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="isEncoding"></param>
    [RelayCommand]
    private void TextHandler(string isEncoding)
    {
        try
        {
            if (OriginalText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            ResultText = isEncoding.Equals("true", StringComparison.CurrentCultureIgnoreCase)
                ? StringHelper.TextToUnicode(OriginalText)
                :StringHelper.UnicodeToText( OriginalText);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"异常：{ex.Message}");
        }
    }
}