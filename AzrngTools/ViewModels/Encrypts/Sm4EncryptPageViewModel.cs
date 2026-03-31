using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AzrngTools.ViewModels.Encrypts;

public partial class Sm4EncryptPageViewModel : ViewModelBase
{
    private readonly ILogger<Sm4EncryptPageViewModel> _logger;
    private readonly IMessageService _messageService;

    public Sm4EncryptPageViewModel(ILogger<Sm4EncryptPageViewModel> logger, IMessageService messageService)
    {
        _logger = logger;
        _messageService = messageService;
        _secret = "BMxA9xjQVLsJGEhD";
        OutTypeValue = (int)OutType.Hex;
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
    /// 密文
    /// </summary>
    [ObservableProperty]
    private string _ciphertext;

    /// <summary>
    /// 输出类型
    /// </summary>
    [ObservableProperty]
    private int _outTypeValue;

    #endregion

    [RelayCommand]
    private void Sm4Handler(string isEncrypt)
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要加密/解密的内容");
                return;
            }

            if (Secret.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入密钥");
                return;
            }

            var outType = OutTypeValue == 0 ? OutType.Base64 : OutType.Hex;
            Ciphertext = isEncrypt.Equals("true")
                ? Sm4Helper.Encrypt(Original, Secret, outType: outType)
                : Sm4Helper.Decrypt(Original, Secret, inputType: outType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"sm4加密失败  message:{ex.Message}");
            _messageService.SendMessage("加密/解密失败");
        }
    }
}