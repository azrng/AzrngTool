namespace AzrngTools.Models;

/// <summary>
/// 硬件信息快照。
/// </summary>
public sealed class HardwareInfoSnapshot
{
    public string Fingerprint { get; set; } = string.Empty;

    public string CpuId { get; set; } = string.Empty;

    public string HardDiskId { get; set; } = string.Empty;

    public string BiosSerial { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public DateTime CachedAtUtc { get; set; }
}
