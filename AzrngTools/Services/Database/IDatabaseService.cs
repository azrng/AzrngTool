using Azrng.Core.Model;
using Azrng.Core.Results;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IDatabaseService
{
    void InvalidateCache();

    Task<IResultModel<DatabaseConnectionTestResult>> TestConnectionAsync(ConnectionConfig? config);

    Task<IResultModel<List<string>>> GetDatabaseNamesAsync(ConnectionConfig config);

    Task<IResultModel<TreeNodeItem?>> LoadDatabaseTreeAsync(ConnectionConfig config);

    Task<IResultModel<List<SchemaModel>>> GetSchemasAsync(ConnectionConfig config);

    Task<IResultModel<List<TableModel>>> GetTablesAsync(ConnectionConfig config, string schemaName);

    Task<IResultModel<List<ViewModel>>> GetViewsAsync(ConnectionConfig config, string schemaName);

    Task<IResultModel<List<StoredProcedureModel>>> GetStoredProceduresAsync(
        ConnectionConfig config,
        string schemaName);

    Task<IResultModel<List<StoredProcedureModel>>> GetFunctionsAsync(
        ConnectionConfig config,
        string schemaName);

    Task<IResultModel<List<ColumnModel>>> GetColumnsAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName);

    Task<IResultModel<List<IndexModel>>> GetIndexesAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName);

    Task<IResultModel<DatabaseTableStatisticsResult>> GetTableStatisticsAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName);

    Task<IResultModel<string>> GetViewDefinitionAsync(
        ConnectionConfig config,
        string schemaName,
        string viewName);

    Task<IResultModel<string>> GetStoredProcedureDefinitionAsync(
        ConnectionConfig config,
        string schemaName,
        string procedureName);

    Task<IResultModel<bool>> UpdateTableCommentAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName,
        string? comment);

    Task<IResultModel<bool>> UpdateColumnCommentAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName,
        string columnName,
        string? comment);

    Task<IResultModel<DatabaseSqlExecutionResult>> ExecuteSqlAsync(
        ConnectionConfig config,
        string sql);
}
