#nullable disable
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
    private readonly IMessageService _messageService;

    public JsonPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
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
    private void FormatJson()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            Original = JsonHelper.JsonFormatter(Original);
        }
        catch (Exception e)
        {
            _messageService.SendMessage($"Json解析失败，请检查 ：{e.Message}");
        }
    }

    /// <summary>
    /// 压缩Json
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

            Original = JsonHelper.JsonCompress(Original);
        }
        catch (Exception e)
        {
            _messageService.SendMessage($"Json解析失败，请检查 ：{e.Message}");
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
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的内容");
                return;
            }

            Original = JsonHelper.EscapeJsonText(Original);
        }
        catch (Exception e)
        {
            _messageService.SendMessage($"Json解析失败，请检查 ：{e.Message}");
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

            var compressed = JsonHelper.JsonCompress(Original);
            Original = JsonHelper.EscapeJsonText(compressed);
        }
        catch (Exception e)
        {
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
    private void ReplaceEscape()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            var result = JsonHelper.UnescapeJsonText(Original);
            Original = result;
        }
        catch (Exception ex)
        {
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
