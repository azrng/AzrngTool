using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;
using DatabaseViewModel = AzrngTools.Models.Database.ViewModel;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseExportPayloadServiceTests
{
    [Fact]
    public async Task BuildPayload_loads_selected_tables_views_and_procedures()
    {
        var databaseService = new FakeDatabaseService();
        var service = new DatabaseExportPayloadService(databaseService);
        var connection = new ConnectionConfig { Name = "pg", DatabaseType = DatabaseType.PostgresSql };
        var selectedObjects = new List<ExportSelectedObjectDto>
        {
            new() { SchemaName = "public", Name = "users", ObjectType = ExportObjectType.Table },
            new() { SchemaName = "public", Name = "active_users", ObjectType = ExportObjectType.View },
            new() { SchemaName = "public", Name = "refresh_user_stats", ObjectType = ExportObjectType.Procedure }
        };

        var result = await service.BuildPayloadAsync(connection, selectedObjects);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalObjectCount);
        Assert.Single(result.Tables);
        Assert.Single(result.Views);
        Assert.Single(result.Procedures);
        Assert.True(result.TableColumnsMap.ContainsKey("public.users"));
        Assert.True(result.TableIndexesMap.ContainsKey("public.users"));
    }

    [Fact]
    public async Task BuildPayload_returns_failure_when_no_objects_match()
    {
        var databaseService = new FakeDatabaseService();
        var service = new DatabaseExportPayloadService(databaseService);
        var connection = new ConnectionConfig { Name = "pg", DatabaseType = DatabaseType.PostgresSql };
        var selectedObjects = new List<ExportSelectedObjectDto>
        {
            new() { SchemaName = "public", Name = "missing", ObjectType = ExportObjectType.View }
        };

        var result = await service.BuildPayloadAsync(connection, selectedObjects);

        Assert.False(result.Success);
        Assert.Equal("未匹配到可导出的对象。", result.Message);
    }

    private sealed class FakeDatabaseService : IDatabaseService
    {
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

        public Task<(bool Success, List<DatabaseViewModel> Views, string Message)> GetViewsAsync(
            ConnectionConfig config,
            string schemaName)
        {
            return Task.FromResult<(bool, List<DatabaseViewModel>, string)>((true,
                new List<DatabaseViewModel>
                {
                    new() { Schema = schemaName, Name = "active_users" }
                },
                "ok"));
        }

        public Task<(bool Success, List<StoredProcedureModel> Procedures, string Message)> GetStoredProceduresAsync(
            ConnectionConfig config,
            string schemaName)
        {
            return Task.FromResult<(bool, List<StoredProcedureModel>, string)>((true,
                new List<StoredProcedureModel>
                {
                    new() { Schema = schemaName, Name = "refresh_user_stats" }
                },
                "ok"));
        }

        public Task<(bool Success, List<ColumnModel> Columns, string Message)> GetColumnsAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            return Task.FromResult<(bool, List<ColumnModel>, string)>((true,
                new List<ColumnModel>
                {
                    new() { Name = "id", OrdinalPosition = 1 }
                },
                "ok"));
        }

        public Task<(bool Success, List<IndexModel> Indexes, string Message)> GetIndexesAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            return Task.FromResult<(bool, List<IndexModel>, string)>((true,
                new List<IndexModel>
                {
                    new() { Name = "pk_users" }
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

        public Task<(bool Success, List<StoredProcedureModel> Functions, string Message)> GetFunctionsAsync(
            ConnectionConfig config,
            string schemaName) =>
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
