#nullable disable
using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace AzrngTools.ViewModels.Encrypts
{
    /// <summary>
    /// Aes加密
    /// </summary>
    public partial class AesEncryptPageViewModel : ViewModelBase
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<DesEncryptPageViewModel> _logger;

        public AesEncryptPageViewModel(IMessageService messageService, ILogger<DesEncryptPageViewModel> logger)
        {
            InputOriginalTypeIndex = (int)SecretType.Text;
            OutTypeIndex = (int)OutType.Base64;
            SecretTypeIndex = (int)SecretType.Text;
            CipherModeIndex = (int)CipherMode.ECB;
            PaddingModeIndex = (int)PaddingMode.PKCS7;
            Secret = "879f803731774546";

            _messageService = messageService;
            _logger = logger;
        }

        #region 属性

        /// <summary>
        /// 原文
        /// </summary>
        [ObservableProperty]
        private string _original;

        /// <summary>
        /// 输入类型
        /// </summary>
        [ObservableProperty]
        private int _inputOriginalTypeIndex;

        /// <summary>
        /// 密钥
        /// </summary>
        [ObservableProperty]
        private string _secret;

        /// <summary>
        /// iv
        /// </summary>
        [ObservableProperty]
        private string _iv;

        /// <summary>
        /// 密钥类型
        /// </summary>
        [ObservableProperty]
        private int _secretTypeIndex;

        /// <summary>
        /// 输出类型
        /// </summary>
        [ObservableProperty]
        private int _outTypeIndex;

        /// <summary>
        /// 加密模式
        /// </summary>
        [ObservableProperty]
        private int _cipherModeIndex;

        /// <summary>
        /// 填充模式
        /// </summary>
        [ObservableProperty]
        private int _paddingModeIndex;

        /// <summary>
        /// 密文
        /// </summary>
        [ObservableProperty]
        private string _ciphertext;

        #endregion

        /// <summary>
        /// AES处理
        /// </summary>
        [RelayCommand]
        private void Handler(string isEncrypt)
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("原始内容不能为空");
                return;
            }

            if (Secret.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("秘钥不能为空");
                return;
            }

            if (CipherModeIndex == 0)
            {
                _messageService.SendMessage("请选择密码模式");
                return;
            }

            if (PaddingModeIndex == 0)
            {
                _messageService.SendMessage("请选择填充模式");
                return;
            }

            if ((CipherMode)CipherModeIndex != CipherMode.ECB && Iv.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入IV");
                return;
            }

            try
            {
                if (isEncrypt.Equals("true"))
                {
                    Ciphertext = AesHelper.Encrypt(Original, Secret, iv: Iv, cipherMode: (CipherMode)CipherModeIndex,
                        paddingMode: (PaddingMode)PaddingModeIndex, outType: (OutType)OutTypeIndex,
                        secretType: (SecretType)SecretTypeIndex);
                }
                else
                {
                    Ciphertext = AesHelper.Decrypt(Original, Secret, iv: Iv, cipherMode: (CipherMode)CipherModeIndex,
                        paddingMode: (PaddingMode)PaddingModeIndex, secretType: (SecretType)SecretTypeIndex,
                        cipherTextType: (OutType)OutTypeIndex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理失败：{ex.Message}");
                _messageService.SendMessage($"处理失败:{ex.Message}");
            }
        }

        /// <summary>
        /// 重置
        /// </summary>
        [RelayCommand]
        private void Reset()
        {
            Original = string.Empty;
            Secret = string.Empty;
            Iv = string.Empty;
            Ciphertext = string.Empty;
        }
    }
}