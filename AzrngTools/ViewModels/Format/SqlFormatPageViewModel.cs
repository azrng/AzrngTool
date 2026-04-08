#nullable disable
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
    private readonly IMessageService _messageService;

    public SqlFormatPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
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
    private void SqlFormat()
    {
        try
        {
            if (OriginText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            OriginText = OriginText.SqlFormat();
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }

    /// <summary>
    /// sql压缩
    /// </summary>
    [RelayCommand]
    private void SqlCompress()
    {
        try
        {
            if (OriginText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要处理的文本");
                return;
            }

            OriginText = TSqlFormatHelper.CompressToString(OriginText);
        }
        catch (Exception ex)
        {
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
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}