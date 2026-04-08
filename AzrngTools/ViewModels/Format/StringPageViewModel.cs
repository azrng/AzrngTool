#nullable disable
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
    private readonly IMessageService _messageService;

    public StringPageViewModel(IMessageService messageService)
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

            Original = StringHelper.CompressText(Original);
        }
        catch (Exception e)
        {
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

            Original = StringHelper.CompressText(Original);
            Original = Original.Replace("\"", @"\""");
        }
        catch (Exception e)
        {
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

            Original = Original.Replace(@"\""", "\"").Replace("\\n", "");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}