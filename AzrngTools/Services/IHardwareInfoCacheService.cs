using AzrngTools.Models;

namespace AzrngTools.Services;

/// <summary>
/// 硬件信息缓存服务。
/// </summary>
public interface IHardwareInfoCacheService
{
    /// <summary>
    /// 获取当前设备的硬件信息快照，优先读取本地缓存。
    /// </summary>
    HardwareInfoSnapshot GetHardwareInfo();
}
