#nullable disable
using AzrngTools.Models;
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.ViewModels.Setting
{
    /// <summary>
    /// 硬件信息 System.Management方法不支持AOT发布
    /// </summary>
    public partial class HardwarePageViewModel : ViewModelBase
    {
        private readonly IHardwareInfoCacheService _hardwareInfoCacheService;
        private readonly IMessageService _messageService;

        public HardwarePageViewModel(IHardwareInfoCacheService hardwareInfoCacheService, IMessageService messageService)
        {
            _hardwareInfoCacheService = hardwareInfoCacheService;
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
                ApplySnapshot(_hardwareInfoCacheService.GetHardwareInfo());
            }
            catch (Exception ex)
            {
                _messageService.SendMessage($"异常：{ex.Message}");
            }
        }

        private void ApplySnapshot(HardwareInfoSnapshot snapshot)
        {
            Fingerprint = snapshot.Fingerprint;
            CpuId = snapshot.CpuId;
            HardDiskId = snapshot.HardDiskId;
            BiosSerial = snapshot.BiosSerial;
            MacAddress = snapshot.MacAddress;
        }
    }
}
