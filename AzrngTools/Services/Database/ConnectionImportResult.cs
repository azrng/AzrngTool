using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public sealed class ConnectionImportResult
{
    public ConnectionImportResult(IReadOnlyList<ConnectionConfig> importedConnections, int skippedCount)
    {
        ImportedConnections = importedConnections;
        ImportedCount = importedConnections.Count;
        SkippedCount = skippedCount;
        Message = SkippedCount > 0
            ? $"已导入 {ImportedCount} 个连接，跳过 {SkippedCount} 个重复项。"
            : $"已导入 {ImportedCount} 个连接。";
    }

    public IReadOnlyList<ConnectionConfig> ImportedConnections { get; }

    public int ImportedCount { get; }

    public int SkippedCount { get; }

    public string Message { get; }
}
