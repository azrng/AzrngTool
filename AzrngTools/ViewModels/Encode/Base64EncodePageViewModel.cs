using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// 文本编码处理
/// </summary>
public partial class Base64EncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public Base64EncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    #region 属性

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _original;

    /// <summary>
    /// 处理后的文本
    /// </summary>
    [ObservableProperty]
    private string _handleText;

    #endregion

    /// <summary>
    /// base64处理
    /// </summary>
    /// <param name="encoding">true编码 false解码</param>
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
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}