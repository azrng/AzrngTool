#nullable disable
using AzrngTools.Utils.Events;
using Common.Windows.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.Setting
{
    /// <summary>
    /// 硬件信息 System.Management方法不支持AOT发布
    /// </summary>
    public partial class HardwarePageViewModel : ViewModelBase
    {
        private readonly IMessageService _messageService;

        public HardwarePageViewModel(IMessageService messageService)
        {
            _messageService = messageService;
            Generate();
        }

        /// <summary>
        /// 设备指纹
        /// </summary>
        [ObservableProperty]
        private string _fingerprint;

        /// <summary>
        /// cpu编号
        /// </summary>
        [ObservableProperty]
        private string _cpuId;

        /// <summary>
        /// 硬盘编号
        /// </summary>
        [ObservableProperty]
        private string _hardDiskId;

        /// <summary>
        /// BIOS序列号
        /// </summary>
        [ObservableProperty]
        private string _biosSerial;

        /// <summary>
        /// mac地址
        /// </summary>
        [ObservableProperty]
        private string _macAddress;

        private void Generate()
        {
            try
            {
                CpuId = HardwareInfo.GetCpuId();
                HardDiskId = HardwareInfo.GetMainDiskId();
                BiosSerial = HardwareInfo.GetBiosSerial();
                MacAddress = HardwareInfo.GetMacAddress();

                Fingerprint = HardwareInfo.GenerateFingerprint(CpuId, HardDiskId, BiosSerial, MacAddress);
            }
            catch (Exception ex)
            {
                _messageService.SendMessage($"异常：{ex.Message}");
            }
        }
    }
}