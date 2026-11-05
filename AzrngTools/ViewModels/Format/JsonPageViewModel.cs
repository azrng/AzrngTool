#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// json格式化
/// </summary>
public partial class JsonPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "format-json";

    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnOriginalChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public JsonPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
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
    /// 格式化json
    /// </summary>
    [RelayCommand]
    private async Task FormatJsonAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            // 大文本解析可能耗时明显，移出 UI 线程避免界面冻结
            var source = Original;
            using (PerfLogScope.Start("JSON格式化", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                Original = await Task.Run(() => JsonHelper.JsonFormatter(source));
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"Json格式化失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"Json解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 压缩Json
    /// </summary>
    [RelayCommand]
    private async Task CompressJsonAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            var source = Original;
            using (PerfLogScope.Start("JSON压缩", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                Original = await Task.Run(() => JsonHelper.JsonCompress(source));
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"Json压缩失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"Json解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 转义Json
    /// </summary>
    [RelayCommand]
    private async Task EscapeJsonAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            var source = Original;
            using (PerfLogScope.Start("JSON 转义", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                Original = await Task.Run(() => JsonHelper.EscapeJsonText(source));
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"Json转义失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"Json转义失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 压缩并转义Json
    /// </summary>
    [RelayCommand]
    private async Task CompressEscapeJsonAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            var source = Original;
            using (PerfLogScope.Start("JSON 转义压缩", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                Original = await Task.Run(() => JsonHelper.EscapeJsonText(JsonHelper.JsonCompress(source)));
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"Json压缩转义失败: {e.Message}\n{e.GetExceptionAndStack()}");
            if (IsLikelyEscapedJsonText(Original))
            {
                _messageService.SendMessage("当前内容看起来已经是转义后的 JSON。请先点击“去除转义”还原，或直接使用当前结果。");
                return;
            }

            _messageService.SendMessage($"Json解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// Json文本去除转义
    /// </summary>
    [RelayCommand]
    private async Task ReplaceEscapeAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            var source = Original;
            using (PerfLogScope.Start("JSON 去转义", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                Original = await Task.Run(() => JsonHelper.UnescapeJsonText(source));
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError($"Json去除转义失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }

    private static bool IsLikelyEscapedJsonText(string text)
    {
        if (text.IsNullOrWhiteSpace() || !text.Contains("\\\"", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var unescaped = JsonHelper.UnescapeJsonText(text);
            JsonHelper.JsonCompress(unescaped);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
