using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// jwt解码器
/// </summary>
public partial class JwtEncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public JwtEncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// 原始内容
    /// </summary>
    [ObservableProperty]
    private string _original;

    /// <summary>
    /// 头部
    /// </summary>
    [ObservableProperty]
    private string _headerText;

    /// <summary>
    /// 载荷
    /// </summary>
    [ObservableProperty]
    private string _payloadText;

    /// <summary>
    /// 文本改变事件
    /// </summary>
    partial void OnOriginalChanged(string value)
    {
        try
        {
            if (value.IsNullOrWhiteSpace())
            {
                return;
            }

            if (!value.Contains("."))
            {
                _messageService.SendMessage("JWT格式有误");
                return;
            }

            value = value.Trim();
            if (value.StartsWith("Bearer "))
            {
                value = value.Replace("Bearer ", "");
            }

            var texts = value.Split('.');
            if (texts.Length < 2)
            {
                _messageService.SendMessage("JWT格式有误");
                return;
            }

            var header = BaseTextHandle(texts[0]).FromBase64Decode();
            var payLoad = BaseTextHandle(texts[1]).FromBase64Decode();
            //var status = BaseUrl(texts[2]).FromBase64Decode();
            HeaderText = JsonHelper.JsonFormatter(header);
            PayloadText = JsonHelper.JsonFormatter(payLoad);
        }
        catch (Exception ex)
        {
            HeaderText = string.Empty;
            PayloadText = string.Empty;
            _messageService.SendMessage($"处理失败:{ex.Message}");
        }
    }

    /// <summary>
    /// 文本补值
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private string BaseTextHandle(string text)
    {
        char[] padding = ['='];
        text = text.TrimEnd(padding).Replace('-', '+').Replace('_', '/').Replace(" ", "+");

        //base字符串必须被4整除，不足的在末尾填充'='号
        if (text.Length % 4 > 0)
        {
            text = text.PadRight(text.Length + 4 - text.Length % 4, '=');
        }

        return text;
    }
}