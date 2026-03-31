namespace AzrngTools.Models;

/// <summary>
/// 单个工具的使用记录。
/// </summary>
public sealed class ToolUsageRecord
{
    public string MenuKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int UseCount { get; set; }

    public DateTime LastUsedUtc { get; set; }
}
