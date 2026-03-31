using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encrypts
{
    public partial class HashPageViewModel : ViewModelBase
    {
        private readonly IMessageService _messageService;

        public HashPageViewModel(IMessageService messageService)
        {
            _messageService = messageService;
            OutTypeValue = (int)OutType.Hex;
        }

        #region 属性

        /// <summary>
        /// 原文
        /// </summary>
        [ObservableProperty]
        private string _original;

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
        /// md5处理
        /// </summary>
        [RelayCommand]
        private void OriginalHandler(string param)
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("原文不能为空");
                return;
            }

            try
            {
                Ciphertext = param switch
                {
                    "0" => Md5Helper.GetMd5Hash(Original, outputType: (OutType)OutTypeValue),
                    "1" => ShaHelper.GetSha1Hash(Original, outputType: (OutType)OutTypeValue),
                    "2" => ShaHelper.GetSha256Hash(Original, outputType: (OutType)OutTypeValue),
                    "3" => ShaHelper.GetSha512Hash(Original, outputType: (OutType)OutTypeValue),
                    "4" => Sm3Helper.GetSm3Hash(Original, outputType: (OutType)OutTypeValue),
                    _ => Ciphertext
                };
            }
            catch (Exception ex)
            {
                _messageService.SendMessage($"处理失败：{ex.Message}");
            }
        }
    }
}