using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.Services.Database;

public class DatabaseExportPayloadService : IDatabaseExportPayloadService, ISingletonDependency
{
    private readonly IDatabaseService _databaseService;

    public DatabaseExportPayloadService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<DatabaseExportPayloadResult> BuildPayloadAsync(
        ConnectionConfig connection,
        IReadOnlyCollection<ExportSelectedObjectDto> selectedObjects,
        Action<string>? progress = null)
    {
        var result = new DatabaseExportPayloadResult { Success = true };

        if (selectedObjects.Count == 0)
        {
            return result.WithFailure("请至少选择一个导出对象。");
        }

        var schemaNames = selectedObjects
            .Select(item => item.SchemaName)
            .Where(schemaName => !string.IsNullOrWhiteSpace(schemaName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var schemaName in schemaNames)
        {
            var tableResult = await LoadSelectedTablesAsync(connection, selectedObjects, schemaName, result, progress);
            if (!tableResult.Success)
            {
                return tableResult;
            }

            var viewResult = await LoadSelectedViewsAsync(connection, selectedObjects, schemaName, result, progress);
            if (!viewResult.Success)
            {
                return viewResult;
            }

            var procedureResult = await LoadSelectedProceduresAsync(connection, selectedObjects, schemaName, result, progress);
            if (!procedureResult.Success)
            {
                return procedureResult;
            }
        }

        return result.TotalObjectCount == 0
            ? result.WithFailure("未匹配到可导出的对象。")
            : new DatabaseExportPayloadResult
            {
                Success = true,
                Tables = result.Tables,
                Views = result.Views,
                Procedures = result.Procedures,
                TableColumnsMap = result.TableColumnsMap,
                TableIndexesMap = result.TableIndexesMap,
                Message = $"已为导出准备 {result.TotalObjectCount} 个对象。"
            };
    }

    private async Task<DatabaseExportPayloadResult> LoadSelectedTablesAsync(
        ConnectionConfig connection,
        IReadOnlyCollection<ExportSelectedObjectDto> selectedObjects,
        string schemaName,
        DatabaseExportPayloadResult result,
        Action<string>? progress)
    {
        var selectedTableNames = GetSelectedNames(selectedObjects, schemaName, ExportObjectType.Table);
        if (selectedTableNames.Count == 0)
        {
            return result;
        }

        progress?.Invoke($"正在加载表 {schemaName}...");
        var (tableSuccess, schemaTables, tableMessage) = await _databaseService.GetTablesAsync(connection, schemaName);
        if (!tableSuccess)
        {
            return result.WithFailure(tableMessage);
        }

        var matchingTables = schemaTables
            .Where(table => selectedTableNames.Contains(table.Name))
            .OrderBy(table => table.Name)
            .ToList();

        var columnTasks = matchingTables.Select(table =>
            _databaseService.GetColumnsAsync(connection, table.Schema, table.Name));
        var indexTasks = matchingTables.Select(table =>
            _databaseService.GetIndexesAsync(connection, table.Schema, table.Name));

        var columnResults = await Task.WhenAll(columnTasks);
        var indexResults = await Task.WhenAll(indexTasks);

        for (var i = 0; i < matchingTables.Count; i++)
        {
            var table = matchingTables[i];
            result.Tables.Add(table);

            var (columnSuccess, columns, columnMessage) = columnResults[i];
            if (!columnSuccess)
            {
                LoggingService.LogWarning($"Column export fallback for {table.Schema}.{table.Name}: {columnMessage}");
                result.TableColumnsMap[BuildTableExportKey(table)] = [];
            }
            else
            {
                result.TableColumnsMap[BuildTableExportKey(table)] = columns.OrderBy(column => column.OrdinalPosition).ToList();
            }

            var (indexSuccess, indexes, indexMessage) = indexResults[i];
            if (!indexSuccess)
            {
                LoggingService.LogWarning($"Index export fallback for {table.Schema}.{table.Name}: {indexMessage}");
                result.TableIndexesMap[BuildTableExportKey(table)] = [];
            }
            else
            {
                result.TableIndexesMap[BuildTableExportKey(table)] = indexes.ToList();
            }

            progress?.Invoke($"正在加载 {table.Schema}.{table.Name}... ({i + 1}/{matchingTables.Count})");
        }

        return result;
    }

    private async Task<DatabaseExportPayloadResult> LoadSelectedViewsAsync(
        ConnectionConfig connection,
        IReadOnlyCollection<ExportSelectedObjectDto> selectedObjects,
        string schemaName,
        DatabaseExportPayloadResult result,
        Action<string>? progress)
    {
        var selectedViewNames = GetSelectedNames(selectedObjects, schemaName, ExportObjectType.View);
        if (selectedViewNames.Count == 0)
        {
            return result;
        }

        progress?.Invoke($"正在加载视图 {schemaName}...");
        var (viewSuccess, schemaViews, viewMessage) = await _databaseService.GetViewsAsync(connection, schemaName);
        if (!viewSuccess)
        {
            return result.WithFailure(viewMessage);
        }

        result.Views.AddRange(schemaViews
            .Where(view => selectedViewNames.Contains(view.Name))
            .OrderBy(view => view.Name));

        return result;
    }

    private async Task<DatabaseExportPayloadResult> LoadSelectedProceduresAsync(
        ConnectionConfig connection,
        IReadOnlyCollection<ExportSelectedObjectDto> selectedObjects,
        string schemaName,
        DatabaseExportPayloadResult result,
        Action<string>? progress)
    {
        var selectedProcedureNames = GetSelectedNames(selectedObjects, schemaName, ExportObjectType.Procedure);
        if (selectedProcedureNames.Count == 0)
        {
            return result;
        }

        progress?.Invoke($"正在加载存储过程 {schemaName}...");
        var (procedureSuccess, schemaProcedures, procedureMessage) =
            await _databaseService.GetStoredProceduresAsync(connection, schemaName);
        if (!procedureSuccess)
        {
            return result.WithFailure(procedureMessage);
        }

        result.Procedures.AddRange(schemaProcedures
            .Where(procedure => selectedProcedureNames.Contains(procedure.Name))
            .OrderBy(procedure => procedure.Name));

        return result;
    }

    private static HashSet<string> GetSelectedNames(
        IEnumerable<ExportSelectedObjectDto> selectedObjects,
        string schemaName,
        ExportObjectType objectType)
    {
        return selectedObjects
            .Where(item => item.ObjectType == objectType &&
                           string.Equals(item.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildTableExportKey(TableModel table)
    {
        return $"{table.Schema}.{table.Name}";
    }
}

internal static class DatabaseExportPayloadResultExtensions
{
    public static DatabaseExportPayloadResult WithFailure(
        this DatabaseExportPayloadResult result,
        string message)
    {
        return new DatabaseExportPayloadResult
        {
            Success = false,
            Tables = result.Tables,
            Views = result.Views,
            Procedures = result.Procedures,
            TableColumnsMap = result.TableColumnsMap,
            TableIndexesMap = result.TableIndexesMap,
            Message = message
        };
    }
}
