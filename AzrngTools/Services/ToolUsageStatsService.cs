using System.Text.Json;

namespace AzrngTools.Services;

/// <summary>
/// 按工具使用频率推荐常用入口，并将统计数据持久化到用户目录。
/// 建议算法：
/// 1. 每次打开工具时累计 UseCount，并更新 LastUsedUtc。
/// 2. 频率分使用 log2(UseCount + 1) 做平滑，避免高频工具无限放大。
/// 3. 最近使用分使用 e^(-days / 7) 衰减，兼顾近期活跃度。
/// 4. 最终分数 = 频率分 * 0.75 + 最近分 * 0.25。
/// </summary>
public sealed class ToolUsageStatsService : IToolUsageStatsService, ISingletonDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _syncRoot = new();
    private readonly string _filePath;
    private ToolUsageStore _store;

    public ToolUsageStatsService()
    {
        _filePath = GetStoreFilePath();
        _store = LoadStore();
    }

    public void RecordToolUsage(string menuKey, string title)
    {
        if (string.IsNullOrWhiteSpace(menuKey))
        {
            return;
        }

        lock (_syncRoot)
        {
            var now = DateTime.UtcNow;
            var record = _store.Records.FirstOrDefault(item => item.MenuKey == menuKey);
            if (record is null)
            {
                record = new ToolUsageRecord
                {
                    MenuKey = menuKey,
                    Title = title,
                    UseCount = 0
                };
                _store.Records.Add(record);
            }

            record.Title = title;
            record.UseCount += 1;
            record.LastUsedUtc = now;

            SaveStore(_store);
        }
    }

    public IReadOnlyList<string> GetTopToolKeys(int maxCount)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        lock (_syncRoot)
        {
            var now = DateTime.UtcNow;
            return _store.Records
                .Where(record => !string.IsNullOrWhiteSpace(record.MenuKey))
                .OrderByDescending(record => CalculateScore(record, now))
                .ThenByDescending(record => record.UseCount)
                .ThenByDescending(record => record.LastUsedUtc)
                .Take(maxCount)
                .Select(record => record.MenuKey)
                .ToList();
        }
    }

    private static double CalculateScore(ToolUsageRecord record, DateTime utcNow)
    {
        var safeUseCount = Math.Max(0, record.UseCount);
        var frequencyScore = Math.Log2(safeUseCount + 1);

        var lastUsedUtc = record.LastUsedUtc == default
            ? DateTime.MinValue
            : DateTime.SpecifyKind(record.LastUsedUtc, DateTimeKind.Utc);
        var daysSinceLastUse = Math.Max(0d, (utcNow - lastUsedUtc).TotalDays);
        var recencyScore = Math.Exp(-daysSinceLastUse / 7d);

        return frequencyScore * 0.75d + recencyScore * 0.25d;
    }

    private ToolUsageStore LoadStore()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ToolUsageStore();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ToolUsageStore>(json, JsonOptions) ?? new ToolUsageStore();
        }
        catch
        {
            return new ToolUsageStore();
        }
    }

    private void SaveStore(ToolUsageStore store)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(store, JsonOptions);
        var tempFilePath = _filePath + ".tmp";
        File.WriteAllText(tempFilePath, json);
        File.Move(tempFilePath, _filePath, true);
    }

    private static string GetStoreFilePath()
    {
        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools");
        return Path.Combine(userDataDirectory, "tool-usage-stats.json");
    }
}
