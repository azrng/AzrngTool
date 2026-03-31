using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AzrngTools.ViewModels.Encrypts
{
    /// <summary>
    /// des对称加密
    /// </summary>
    public partial class DesEncryptPageViewModel : ViewModelBase
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<DesEncryptPageViewModel> _logger;

        public DesEncryptPageViewModel(IMessageService messageService, ILogger<DesEncryptPageViewModel> logger)
        {
            OutTypeValue = (int)OutType.Hex;
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

        /// <summary>
        /// DES处理
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

            try
            {
                var flag = isEncrypt.Equals("true", StringComparison.CurrentCultureIgnoreCase);

                // ecb模式 hax编码
                Ciphertext = flag ? DesHelper.Encrypt(Original, Secret) : DesHelper.Decrypt(Original, Secret);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理失败：{ex.Message}");
                _messageService.SendMessage($"处理失败:{ex.Message}");
            }
        }
    }
}