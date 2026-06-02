using Azrng.Core.Model;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IDatabaseService
{
    void InvalidateCache();

    Task<(bool Success, string Message, string? Suggestion)> TestConnectionAsync(ConnectionConfig? config);

    Task<(bool Success, List<string> Databases, string Message)> GetDatabaseNamesAsync(ConnectionConfig config);

    Task<(bool Success, TreeNodeItem? RootNode, string Message)> LoadDatabaseTreeAsync(ConnectionConfig config);

    Task<(bool Success, List<SchemaModel> Schemas, string Message)> GetSchemasAsync(ConnectionConfig config);

    Task<(bool Success, List<TableModel> Tables, string Message)> GetTablesAsync(ConnectionConfig config, string schemaName);

    Task<(bool Success, List<ViewModel> Views, string Message)> GetViewsAsync(ConnectionConfig config, string schemaName);

    Task<(bool Success, List<StoredProcedureModel> Procedures, string Message)> GetStoredProceduresAsync(
        ConnectionConfig config,
        string schemaName);

    Task<(bool Success, List<StoredProcedureModel> Functions, string Message)> GetFunctionsAsync(
        ConnectionConfig config,
        string schemaName);

    Task<(bool Success, List<ColumnModel> Columns, string Message)> GetColumnsAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName);

    Task<(bool Success, List<IndexModel> Indexes, string Message)> GetIndexesAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName);

    Task<(bool Success, long RowCount, DateTime? CreateTime, DateTime? ModifyTime, string Message)> GetTableStatisticsAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName);

    Task<(bool Success, string Definition, string Message)> GetViewDefinitionAsync(
        ConnectionConfig config,
        string schemaName,
        string viewName);

    Task<(bool Success, string Definition, string Message)> GetStoredProcedureDefinitionAsync(
        ConnectionConfig config,
        string schemaName,
        string procedureName);

    Task<(bool Success, string Message)> UpdateTableCommentAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName,
        string? comment);

    Task<(bool Success, string Message)> UpdateColumnCommentAsync(
        ConnectionConfig config,
        string schemaName,
        string tableName,
        string columnName,
        string? comment);

    Task<(bool Success, bool HasResultSet, List<string> Columns, List<List<string>> Rows, int AffectedRows, string Message)> ExecuteSqlAsync(
        ConnectionConfig config,
        string sql);
}
