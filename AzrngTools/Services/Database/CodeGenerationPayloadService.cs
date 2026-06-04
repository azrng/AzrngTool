using Azrng.Core.Results;
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
        var tableResult = await _databaseService.GetTablesAsync(connection, schemaName);
        if (!tableResult.IsSuccess)
        {
            return new CodeGenerationPayloadResult
            {
                Success = false,
                Message = tableResult.Message
            };
        }

        var tables = tableResult.DataOrEmpty();
        var tableColumnsMap = new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables.OrderBy(table => table.Name))
        {
            progress?.Invoke($"正在加载 {table.Schema}.{table.Name} 的字段...");
            var columnResult = await _databaseService.GetColumnsAsync(connection, table.Schema, table.Name);
            if (!columnResult.IsSuccess)
            {
                LoggingService.LogWarning($"Code generation column fallback for {table.Schema}.{table.Name}: {columnResult.Message}");
                tableColumnsMap[table.Name] = [];
                continue;
            }

            tableColumnsMap[table.Name] = columnResult.DataOrEmpty().OrderBy(column => column.OrdinalPosition).ToList();
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
