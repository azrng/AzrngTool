using System.Text.Json;
using System.Text.Json.Serialization;
using AzrngTools.Models;
using Common.Windows.Core;

namespace AzrngTools.Services;

/// <summary>
/// 硬件信息本地缓存服务，避免每次打开页面都重复采集系统信息。
/// </summary>
public sealed partial class HardwareInfoCacheService : IHardwareInfoCacheService, ISingletonDependency
{
    private readonly object _syncRoot = new();
    private readonly string _filePath;
    private HardwareInfoSnapshot? _cachedSnapshot;

    public HardwareInfoCacheService()
    {
        _filePath = GetStoreFilePath();
        _cachedSnapshot = LoadSnapshot();
    }

    public HardwareInfoSnapshot GetHardwareInfo()
    {
        lock (_syncRoot)
        {
            if (IsUsable(_cachedSnapshot))
            {
                return _cachedSnapshot!;
            }

            var snapshot = CollectSnapshot();
            _cachedSnapshot = snapshot;
            SaveSnapshot(snapshot);
            return snapshot;
        }
    }

    public HardwareInfoSnapshot RefreshHardwareInfo()
    {
        lock (_syncRoot)
        {
            var snapshot = CollectSnapshot();
            _cachedSnapshot = snapshot;
            SaveSnapshot(snapshot);
            return snapshot;
        }
    }

    private HardwareInfoSnapshot? LoadSnapshot()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize(json, HardwareInfoJsonContext.Default.HardwareInfoSnapshot);
            return IsUsable(snapshot) ? snapshot : null;
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"加载硬件信息缓存失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            return null;
        }
    }

    private void SaveSnapshot(HardwareInfoSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(snapshot, HardwareInfoJsonContext.Default.HardwareInfoSnapshot);
            var tempFilePath = _filePath + ".tmp";
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _filePath, true);
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"保存硬件信息缓存失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
        }
    }

    private static HardwareInfoSnapshot CollectSnapshot()
    {
        var cpuId = HardwareInfo.GetCpuId() ?? string.Empty;
        var hardDiskId = HardwareInfo.GetMainDiskId() ?? string.Empty;
        var biosSerial = HardwareInfo.GetBiosSerial() ?? string.Empty;
        var macAddress = HardwareInfo.GetMacAddress() ?? string.Empty;

        return new HardwareInfoSnapshot
        {
            CpuId = cpuId,
            HardDiskId = hardDiskId,
            BiosSerial = biosSerial,
            MacAddress = macAddress,
            Fingerprint = HardwareInfo.GenerateFingerprint(cpuId, hardDiskId, biosSerial, macAddress) ?? string.Empty,
            CachedAtUtc = DateTime.UtcNow
        };
    }

    private static bool IsUsable(HardwareInfoSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(snapshot.Fingerprint)
               || !string.IsNullOrWhiteSpace(snapshot.CpuId)
               || !string.IsNullOrWhiteSpace(snapshot.HardDiskId)
               || !string.IsNullOrWhiteSpace(snapshot.BiosSerial)
               || !string.IsNullOrWhiteSpace(snapshot.MacAddress);
    }

    private static string GetStoreFilePath()
    {
        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools");
        return Path.Combine(userDataDirectory, "hardware-info-cache.json");
    }

    // 源生成 JSON 上下文：AOT 发布下不能用反射序列化
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(HardwareInfoSnapshot))]
    private sealed partial class HardwareInfoJsonContext : JsonSerializerContext
    {
    }
}
