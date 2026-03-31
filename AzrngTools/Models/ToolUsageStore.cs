namespace AzrngTools.Models;

/// <summary>
/// 工具使用统计持久化模型。
/// </summary>
public sealed class ToolUsageStore
{
    public List<ToolUsageRecord> Records { get; set; } = [];
}
