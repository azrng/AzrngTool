#nullable disable
using AzrngTools.Services;
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
        private readonly ILogger<AesEncryptPageViewModel> _logger;

        /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
        private const string PageId = "encrypts-aes";

        private readonly IPageInputStoreService _pageInputStore;

        // 构造期默认值赋值与恢复赋值都会触发 OnXxxChanged，置位跳过保存提交，避免默认值快照覆盖已有存档或恢复即回写
        private bool _isRestoring;

        public AesEncryptPageViewModel(IMessageService messageService, ILogger<AesEncryptPageViewModel> logger, IPageInputStoreService pageInputStore)
        {
            _isRestoring = true;
            InputOriginalTypeIndex = (int)SecretType.Text;
            OutTypeIndex = (int)OutType.Base64;
            SecretTypeIndex = (int)SecretType.Text;
            CipherModeIndex = (int)CipherMode.ECB;
            PaddingModeIndex = (int)PaddingMode.PKCS7;
            Secret = "879f803731774546";

            _messageService = messageService;
            _logger = logger;
            _pageInputStore = pageInputStore;
            RestoreInputs();
            _isRestoring = false;
        }

        private void SaveInputs()
        {
            _pageInputStore.Save(PageId, new Dictionary<string, string?>
            {
                // Secret 与 Iv 属敏感键，绝不写入存档
                [nameof(Original)] = PageInputValues.Encode(Original),
                [nameof(InputOriginalTypeIndex)] = PageInputValues.Encode(InputOriginalTypeIndex),
                [nameof(SecretTypeIndex)] = PageInputValues.Encode(SecretTypeIndex),
                [nameof(OutTypeIndex)] = PageInputValues.Encode(OutTypeIndex),
                [nameof(CipherModeIndex)] = PageInputValues.Encode(CipherModeIndex),
                [nameof(PaddingModeIndex)] = PageInputValues.Encode(PaddingModeIndex),
            });
        }

        private void RestoreInputs()
        {
            var saved = _pageInputStore.TryLoad(PageId);
            if (saved.Count == 0)
            {
                return;
            }

            _isRestoring = true;
            try
            {
                if (saved.TryGetValue(nameof(Original), out var original))
                {
                    Original = original;
                }

                if (saved.TryGetValue(nameof(InputOriginalTypeIndex), out var inputOriginalTypeIndex))
                {
                    InputOriginalTypeIndex = PageInputValues.DecodeInt32(inputOriginalTypeIndex, InputOriginalTypeIndex);
                }

                if (saved.TryGetValue(nameof(SecretTypeIndex), out var secretTypeIndex))
                {
                    SecretTypeIndex = PageInputValues.DecodeInt32(secretTypeIndex, SecretTypeIndex);
                }

                if (saved.TryGetValue(nameof(OutTypeIndex), out var outTypeIndex))
                {
                    OutTypeIndex = PageInputValues.DecodeInt32(outTypeIndex, OutTypeIndex);
                }

                if (saved.TryGetValue(nameof(CipherModeIndex), out var cipherModeIndex))
                {
                    CipherModeIndex = PageInputValues.DecodeInt32(cipherModeIndex, CipherModeIndex);
                }

                if (saved.TryGetValue(nameof(PaddingModeIndex), out var paddingModeIndex))
                {
                    PaddingModeIndex = PageInputValues.DecodeInt32(paddingModeIndex, PaddingModeIndex);
                }
            }
            finally
            {
                _isRestoring = false;
            }
        }

        partial void OnOriginalChanged(string value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        partial void OnInputOriginalTypeIndexChanged(int value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        partial void OnSecretTypeIndexChanged(int value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        partial void OnOutTypeIndexChanged(int value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        partial void OnCipherModeIndexChanged(int value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
        }

        partial void OnPaddingModeIndexChanged(int value)
        {
            if (_isRestoring)
            {
                return;
            }

            SaveInputs();
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
                using var perf = PerfLogScope.Start("AES 加解密", $"输入 {PerfLogScope.DescribeSize(Original)}");
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