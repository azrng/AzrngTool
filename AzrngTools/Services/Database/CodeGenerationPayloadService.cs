using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public class CodeGenerationPayloadService : ICodeGenerationPayloadService, ISingletonDependency
{
    private readonly IDatabaseService _databaseService;

    public CodeGenerationPayloadService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<CodeGenerationPayloadResult> BuildPayloadAsync(
        ConnectionConfig connection,
        string schemaName,
        Action<string>? progress = null)
    {
        var (tableSuccess, tables, tableMessage) = await _databaseService.GetTablesAsync(connection, schemaName);
        if (!tableSuccess)
        {
            return new CodeGenerationPayloadResult
            {
                Success = false,
                Message = tableMessage
            };
        }

        var tableColumnsMap = new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables.OrderBy(table => table.Name))
        {
            progress?.Invoke($"正在加载 {table.Schema}.{table.Name} 的字段...");
            var (columnSuccess, columns, columnMessage) =
                await _databaseService.GetColumnsAsync(connection, table.Schema, table.Name);
            if (!columnSuccess)
            {
                LoggingService.LogWarning($"Code generation column fallback for {table.Schema}.{table.Name}: {columnMessage}");
                tableColumnsMap[table.Name] = [];
                continue;
            }

            tableColumnsMap[table.Name] = columns.OrderBy(column => column.OrdinalPosition).ToList();
        }

        return new CodeGenerationPayloadResult
        {
            Success = true,
            Tables = tables,
            TableColumnsMap = tableColumnsMap,
            Message = $"Prepared {tables.Count} tables for code generation."
        };
    }
}
