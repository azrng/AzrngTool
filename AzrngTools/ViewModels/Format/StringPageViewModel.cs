#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// 字符串格式化操作
/// </summary>
public partial class StringPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "format-string";

    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnOriginalChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public StringPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        RestoreInputs();
    }

    partial void OnOriginalChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
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

    #region 属性

    /// <summary>
    /// 原始文本
    /// </summary>
    [ObservableProperty]
    private string _original;

    #endregion

    /// <summary>
    /// 压缩字符串
    /// </summary>
    [RelayCommand]
    private void CompressJson()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            using (PerfLogScope.Start("字符串压缩", $"输入 {PerfLogScope.DescribeSize(Original.Length)}"))
            {
                Original = StringHelper.CompressText(Original);
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"字符串压缩失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 转义字符串
    /// </summary>
    [RelayCommand]
    private void EscapeJson()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            Original = Original.Replace("\"", @"\""");
        }
        catch (Exception e)
        {
            AppLog.LogError($"字符串转义失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 压缩并转义Json
    /// </summary>
    [RelayCommand]
    private void CompressEscapeJson()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            using (PerfLogScope.Start("字符串压缩转义", $"输入 {PerfLogScope.DescribeSize(Original.Length)}"))
            {
                Original = StringHelper.CompressText(Original);
                Original = Original.Replace("\"", @"\""");
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"字符串压缩转义失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 去除转义
    /// </summary>
    [RelayCommand]
    private void ReplaceEscape()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            // 与转义方向对称：\n 还原为真实换行而非整段删除
            Original = Original.Replace(@"\""", "\"").Replace("\\n", "\n");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"字符串去除转义失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}