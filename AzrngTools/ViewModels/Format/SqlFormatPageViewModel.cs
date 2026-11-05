#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// sql格式化工具
/// </summary>
public partial class SqlFormatPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "format-sql";

    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnOriginTextChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public SqlFormatPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        RestoreInputs();
    }

    partial void OnOriginTextChanged(string value)
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
            [nameof(OriginText)] = PageInputValues.Encode(OriginText),
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
            if (saved.TryGetValue(nameof(OriginText), out var originText))
            {
                OriginText = originText;
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    /// <summary>
    /// 原始内容
    /// </summary>
    [ObservableProperty]
    private string _originText;

    /// <summary>
    /// sql格式化
    /// </summary>
    [RelayCommand]
    private async Task SqlFormatAsync()
    {
        try
        {
            if (OriginText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            // TSql 解析器逐 token 处理，大脚本耗时明显，移出 UI 线程避免界面冻结
            var source = OriginText;
            using (PerfLogScope.Start("SQL格式化", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                OriginText = await Task.Run(() => source.SqlFormat());
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError($"SQL格式化失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }

    /// <summary>
    /// sql压缩
    /// </summary>
    [RelayCommand]
    private async Task SqlCompressAsync()
    {
        try
        {
            if (OriginText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            var source = OriginText;
            using (PerfLogScope.Start("SQL压缩", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                OriginText = await Task.Run(() => TSqlFormatHelper.CompressToString(source));
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError($"SQL压缩失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 转义Json
    /// </summary>
    [RelayCommand]
    private void EscapeJson()
    {
        try
        {
            if (OriginText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            OriginText = OriginText.Replace("\"", @"\""");
        }
        catch (Exception e)
        {
            AppLog.LogError($"SQL转义失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{e.Message}");
        }
    }

    /// <summary>
    /// 去除sql转义
    /// </summary>
    [RelayCommand]
    private void ReplaceEscape()
    {
        try
        {
            if (OriginText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            OriginText = OriginText.Replace(@"\""", "\"").Replace("\\n", "");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"SQL去除转义失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}