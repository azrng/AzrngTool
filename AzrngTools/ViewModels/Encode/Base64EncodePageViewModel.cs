#nullable disable
using System.Text;
using Avalonia.Controls;
using AzrngTools.Services;
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

    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "encode-base64";

    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnOriginalChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public Base64EncodePageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        RestoreInputs();
    }

    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            [nameof(Original)] = PageInputValues.Encode(Original),
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
            if (saved.TryGetValue(nameof(Original), out var original))
            {
                Original = original;
            }
        }
        finally
        {
            _isRestoring = false;
        }
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

            using var perf = PerfLogScope.Start("Base64 编解码", $"输入 {PerfLogScope.DescribeSize(Original)}");
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
            AppLog.LogError($"Base64编码/解码失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
            AppLog.LogError($"复制结果失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
            AppLog.LogError($"从剪贴板粘贴失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
        // 恢复赋值期间只跳过存档提交，原有统计通知逻辑仍要执行以刷新恢复后的计数
        if (!_isRestoring)
        {
            SaveInputs();
        }

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
