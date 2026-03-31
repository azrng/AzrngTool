using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AzrngTools.ViewModels.Encrypts;

/// <summary>
/// Hmac Hash处理
/// </summary>
public partial class HmacHashPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<HmacHashPageViewModel> _logger;

    public HmacHashPageViewModel(IMessageService messageService, ILogger<HmacHashPageViewModel> logger)
    {
        _messageService = messageService;
        _logger = logger;

        OutTypeValue = (int)OutType.Hex;
        Secret = Guid.NewGuid().ToString("N");
    }

    #region 属性

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _original;

    /// <summary>
    /// 密钥
    /// </summary>
    [ObservableProperty]
    private string _secret;

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _ciphertext;

    /// <summary>
    /// 输出类型
    /// </summary>
    [ObservableProperty]
    private int _outTypeValue;

    #endregion

    /// <summary>
    /// hmac 哈希处理
    /// </summary>
    [RelayCommand]
    private void Handler(string param)
    {
        if (Original.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("原始内容不能为空");
            return;
        }

        if (Secret.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("密钥不能为空");
            return;
        }

        try
        {
            Ciphertext = param switch
            {
                "0" => Md5Helper.GetHmacMd5Hash(Original, Secret, (OutType)OutTypeValue),
                "1" => ShaHelper.GetHmacSha1Hash(Original, Secret, (OutType)OutTypeValue),
                "2" => ShaHelper.GetHmacSha256Hash(Original, Secret, (OutType)OutTypeValue),
                "3" => ShaHelper.GetHmacSha512Hash(Original, Secret, (OutType)OutTypeValue),
                _ => Ciphertext
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理失败：{ex.Message}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}