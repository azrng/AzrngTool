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
            OriginText = await Task.Run(() => source.SqlFormat());
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"SQL格式化失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
            OriginText = await Task.Run(() => TSqlFormatHelper.CompressToString(source));
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"SQL压缩失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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
            LocalLogHelper.LogError($"SQL转义失败: {e.Message}\n{e.GetExceptionAndStack()}");
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
            LocalLogHelper.LogError($"SQL去除转义失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}