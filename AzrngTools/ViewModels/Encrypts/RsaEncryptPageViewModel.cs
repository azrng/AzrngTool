#nullable disable
using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AzrngTools.ViewModels.Encrypts;

/// <summary>
/// ras加解密
/// </summary>
public partial class RsaEncryptPageViewModel : ViewModelBase
{
    private readonly ILogger<RsaEncryptPageViewModel> _logger;
    private readonly IMessageService _messageService;

    public RsaEncryptPageViewModel(ILogger<RsaEncryptPageViewModel> logger, IMessageService messageService)
    {
        _logger = logger;
        _messageService = messageService;

        RsaKeyTypeValue = (int)RSAKeyType.PEM;
        OutTypeValue = (int)OutType.Base64;
    }

    #region 属性

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _original;

    /// <summary>
    /// 密文
    /// </summary>
    [ObservableProperty]
    private string _ciphertext;

    /// <summary>
    /// 密钥类型
    /// </summary>
    [ObservableProperty]
    private int _rsaKeyTypeValue;

    /// <summary>
    /// 公钥
    /// </summary>
    [ObservableProperty]
    private string _publicKey;

    /// <summary>
    /// 私钥
    /// </summary>
    [ObservableProperty]
    private string _privateKey;

    /// <summary>
    /// 输入输出类型
    /// </summary>
    [ObservableProperty]
    private int _outTypeValue;

    #endregion

    [RelayCommand]
    private void EncryptHandler(string isEncrypt)
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要加密/解密的内容");
                return;
            }

            if (isEncrypt == "0")
            {
                if (PublicKey.IsNullOrWhiteSpace())
                {
                    _messageService.SendMessage("请输入密钥");
                    return;
                }

                Ciphertext = RsaHelper.Encrypt(Original, PublicKey, (RSAKeyType)RsaKeyTypeValue, (OutType)OutTypeValue);
            }
            else
            {
                if (PrivateKey.IsNullOrWhiteSpace())
                {
                    _messageService.SendMessage("请输入密钥");
                    return;
                }

                Ciphertext = RsaHelper.Decrypt(Original, PrivateKey, (RSAKeyType)RsaKeyTypeValue, RsaKeyFormat.PKCS1,
                    (OutType)OutTypeValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Rsa加密/解密失败  message:{ex.Message}");
            _messageService.SendMessage($"加密/解密失败:{ex.Message}");
        }
    }

    /// <summary>
    /// 生成密钥
    /// </summary>
    [RelayCommand]
    private void GenerateSecret()
    {
        try
        {
            var (publicKey, privateKey) = RsaKeyTypeValue == (int)RSAKeyType.Xml
                ? RsaHelper.ExportXmlRsaKey()
                : RsaHelper.ExportPemRsaKey(RsaKeyFormat.PKCS1);

            PublicKey = publicKey;
            PrivateKey = privateKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"生成密钥失败  message:{ex.Message}");
            _messageService.SendMessage($"生成失败：{ex.Message}");
        }
    }
}