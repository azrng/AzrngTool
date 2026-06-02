using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using DatabaseViewModel = AzrngTools.Models.Database.ViewModel;

namespace AzrngTools.Tests.Services.Database;

public class CodeGenerationPayloadServiceTests
{
    [Fact]
    public async Task BuildPayload_loads_tables_and_columns()
    {
        var databaseService = new FakeDatabaseService();
        var service = new CodeGenerationPayloadService(databaseService);
        var connection = new ConnectionConfig { Name = "pg", DatabaseType = DatabaseType.PostgresSql };

        var result = await service.BuildPayloadAsync(connection, "public");

        Assert.True(result.Success);
        Assert.Single(result.Tables);
        Assert.True(result.TableColumnsMap.ContainsKey("users"));
        Assert.Equal(["id", "name"], result.TableColumnsMap["users"].Select(column => column.Name));
    }

    [Fact]
    public async Task BuildPayload_keeps_table_when_column_loading_fails()
    {
        var databaseService = new FakeDatabaseService { FailColumns = true };
        var service = new CodeGenerationPayloadService(databaseService);
        var connection = new ConnectionConfig { Name = "pg", DatabaseType = DatabaseType.PostgresSql };

        var result = await service.BuildPayloadAsync(connection, "public");

        Assert.True(result.Success);
        Assert.Single(result.Tables);
        Assert.Empty(result.TableColumnsMap["users"]);
    }

    private sealed class FakeDatabaseService : IDatabaseService
    {
        public bool FailColumns { get; init; }

        public void InvalidateCache()
        {
        }

        public Task<(bool Success, List<TableModel> Tables, string Message)> GetTablesAsync(
            ConnectionConfig config,
            string schemaName)
        {
            return Task.FromResult<(bool, List<TableModel>, string)>((true,
                new List<TableModel>
                {
                    new() { Schema = schemaName, Name = "users" }
                },
                "ok"));
        }

        public Task<(bool Success, List<ColumnModel> Columns, string Message)> GetColumnsAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            if (FailColumns)
            {
                return Task.FromResult<(bool, List<ColumnModel>, string)>((false, [], "columns failed"));
            }

            return Task.FromResult<(bool, List<ColumnModel>, string)>((true,
                new List<ColumnModel>
                {
                    new() { Name = "name", OrdinalPosition = 2 },
                    new() { Name = "id", OrdinalPosition = 1 }
                },
                "ok"));
        }

        public Task<(bool Success, string Message, string? Suggestion)> TestConnectionAsync(ConnectionConfig? config) =>
            throw new NotSupportedException();

        public Task<(bool Success, List<string> Databases, string Message)> GetDatabaseNamesAsync(ConnectionConfig config) =>
            throw new NotSupportedException();

        public Task<(bool Success, TreeNodeItem? RootNode, string Message)> LoadDatabaseTreeAsync(ConnectionConfig config) =>
            throw new NotSupportedException();

        public Task<(bool Success, List<SchemaModel> Schemas, string Message)> GetSchemasAsync(ConnectionConfig config) =>
            throw new NotSupportedException();

        public Task<(bool Success, List<DatabaseViewModel> Views, string Message)> GetViewsAsync(ConnectionConfig config, string schemaName) =>
            throw new NotSupportedException();

        public Task<(bool Success, List<StoredProcedureModel> Procedures, string Message)> GetStoredProceduresAsync(
            ConnectionConfig config,
            string schemaName) =>
            throw new NotSupportedException();

        public Task<(bool Success, List<StoredProcedureModel> Functions, string Message)> GetFunctionsAsync(
            ConnectionConfig config,
            string schemaName) =>
            throw new NotSupportedException();

        public Task<(bool Success, List<IndexModel> Indexes, string Message)> GetIndexesAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName) =>
            throw new NotSupportedException();

        public Task<(bool Success, long RowCount, DateTime? CreateTime, DateTime? ModifyTime, string Message)> GetTableStatisticsAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName) =>
            throw new NotSupportedException();

        public Task<(bool Success, string Definition, string Message)> GetViewDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string viewName) =>
            throw new NotSupportedException();

        public Task<(bool Success, string Definition, string Message)> GetStoredProcedureDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string procedureName) =>
            throw new NotSupportedException();

        public Task<(bool Success, string Message)> UpdateTableCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string? comment) =>
            throw new NotSupportedException();

        public Task<(bool Success, string Message)> UpdateColumnCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string columnName,
            string? comment) =>
            throw new NotSupportedException();

        public Task<(bool Success, bool HasResultSet, List<string> Columns, List<List<string>> Rows, int AffectedRows, string Message)> ExecuteSqlAsync(
            ConnectionConfig config,
            string sql) =>
            throw new NotSupportedException();
    }
}
