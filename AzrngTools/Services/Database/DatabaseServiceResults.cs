namespace AzrngTools.Services.Database;

public sealed record DatabaseConnectionTestResult(string? Suggestion);

public sealed record DatabaseTableStatisticsResult(
    long RowCount,
    DateTime? CreateTime,
    DateTime? ModifyTime);

public sealed record DatabaseSqlExecutionResult(
    bool HasResultSet,
    List<string> Columns,
    List<List<string>> Rows,
    int AffectedRows);
