#nullable disable
using AzrngTools.Models;
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
            _ = InitializeAsync();
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

        /// <summary>
        /// 异步加载硬件信息。WMI 采集可能阻塞秒级，移到后台线程执行避免冻结首次导航。
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                var snapshot = await Task.Run(_hardwareInfoCacheService.GetHardwareInfo);
                ApplySnapshot(snapshot);
            }
            catch (Exception ex)
            {
                LocalLogHelper.LogError($"获取硬件信息失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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

        [RelayCommand]
        private async Task RefreshHardwareInfoAsync()
        {
            try
            {
                var snapshot = await Task.Run(_hardwareInfoCacheService.RefreshHardwareInfo);
                ApplySnapshot(snapshot);
                _messageService.SendMessage("已刷新本机硬件信息缓存。");
            }
            catch (Exception ex)
            {
                LocalLogHelper.LogError($"刷新硬件信息失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
                _messageService.SendMessage($"刷新失败：{ex.Message}");
            }
        }
    }
}
