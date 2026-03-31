namespace AzrngTools.Services;

/// <summary>
/// 工具使用频率统计服务。
/// </summary>
public interface IToolUsageStatsService
{
    /// <summary>
    /// 记录工具被打开一次。
    /// </summary>
    void RecordToolUsage(string menuKey, string title);

    /// <summary>
    /// 获取推荐展示的常用工具键。
    /// </summary>
    IReadOnlyList<string> GetTopToolKeys(int maxCount);
}
