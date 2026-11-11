#nullable disable
using Avalonia.Controls;
using AzrngTools.Services;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// 文本编码组合
/// </summary>
public partial class EncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "encode-workbench";

    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnOriginalTextChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public EncodePageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        RestoreInputs();
    }

    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            [nameof(OriginalText)] = PageInputValues.Encode(OriginalText),
        });
    }

    private void RestoreInputs()
    {
        var saved = _pageInputStore.TryLoad(PageId);
        if (saved.Count == 0)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            if (saved.TryGetValue(nameof(OriginalText), out var originalText))
            {
                OriginalText = originalText;
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    public bool HasResult => !ResultText.IsNullOrWhiteSpace();

    partial void OnOriginalTextChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnResultTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

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
            AppLog.LogError($"URL编码/解码处理失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理异常：{ex.Message}");
        }
    }

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
                : StringHelper.UnicodeToText(OriginalText);
        }
        catch (Exception ex)
        {
            AppLog.LogError($"Unicode文本处理失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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

            // SetTextAsync 回报真实写入结果：剪贴板不可用/被占用时不再提示"已复制"
            if (await ClipboardHelper.SetTextAsync(GetTopLevel(), ResultText))
            {
                _messageService.SendMessage("已复制到剪贴板");
            }
            else
            {
                _messageService.SendMessage("复制失败：剪贴板不可用");
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError($"复制结果失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
