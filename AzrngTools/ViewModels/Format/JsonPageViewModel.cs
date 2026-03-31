using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;

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

            Original = Original.Replace("\"", @"\""");
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

            Original = JsonHelper.JsonCompress(Original);
            Original = Original.Replace("\"", @"\""");
        }
        catch (Exception e)
        {
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

            // 1. 处理多重转义的引号
            string result = Original.Replace("\\\\\\\"", "\"");

            // 2. 处理普通转义的引号
            result = result.Replace("\\\"", "\"");

            // 3. 处理换行符
            result = result.Replace("\\n", "\n")
                           .Replace("\\r", "\r");

            // 4. 处理其他常见转义字符
            result = result.Replace("\\t", "\t")
                           .Replace("\\\\", "\\")
                           .Replace("\\/", "/")
                           .Replace("\\b", "\b")
                           .Replace("\\f", "\f");

            // 5. 处理HTML标签中的引号
            result = Regex.Replace(result, @"<([^>]*)\\""([^>]*)>", "<$1\"$2>");

            Original = result;

            // 6. 格式化JSON
            Original = JsonHelper.JsonFormatter(result);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}