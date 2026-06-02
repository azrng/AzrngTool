using Azrng.Core.Model;
using Azrng.Core.Results;
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

        public Task<IResultModel<List<TableModel>>> GetTablesAsync(
            ConnectionConfig config,
            string schemaName)
        {
            return Task.FromResult(Success(
                new List<TableModel>
                {
                    new() { Schema = schemaName, Name = "users" }
                },
                "ok"));
        }

        public Task<IResultModel<List<DatabaseViewModel>>> GetViewsAsync(
            ConnectionConfig config,
            string schemaName)
        {
            return Task.FromResult(Success(
                new List<DatabaseViewModel>
                {
                    new() { Schema = schemaName, Name = "active_users" }
                },
                "ok"));
        }

        public Task<IResultModel<List<StoredProcedureModel>>> GetStoredProceduresAsync(
            ConnectionConfig config,
            string schemaName)
        {
            return Task.FromResult(Success(
                new List<StoredProcedureModel>
                {
                    new() { Schema = schemaName, Name = "refresh_user_stats" }
                },
                "ok"));
        }

        public Task<IResultModel<List<ColumnModel>>> GetColumnsAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            return Task.FromResult(Success(
                new List<ColumnModel>
                {
                    new() { Name = "id", OrdinalPosition = 1 }
                },
                "ok"));
        }

        public Task<IResultModel<List<IndexModel>>> GetIndexesAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            return Task.FromResult(Success(
                new List<IndexModel>
                {
                    new() { Name = "pk_users" }
                },
                "ok"));
        }

        public Task<IResultModel<DatabaseConnectionTestResult>> TestConnectionAsync(ConnectionConfig? config) =>
            throw new NotSupportedException();

        public Task<IResultModel<List<string>>> GetDatabaseNamesAsync(ConnectionConfig config) =>
            throw new NotSupportedException();

        public Task<IResultModel<TreeNodeItem?>> LoadDatabaseTreeAsync(ConnectionConfig config) =>
            throw new NotSupportedException();

        public Task<IResultModel<List<SchemaModel>>> GetSchemasAsync(ConnectionConfig config) =>
            throw new NotSupportedException();

        public Task<IResultModel<List<StoredProcedureModel>>> GetFunctionsAsync(
            ConnectionConfig config,
            string schemaName) =>
            throw new NotSupportedException();

        public Task<IResultModel<DatabaseTableStatisticsResult>> GetTableStatisticsAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName) =>
            throw new NotSupportedException();

        public Task<IResultModel<string>> GetViewDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string viewName) =>
            throw new NotSupportedException();

        public Task<IResultModel<string>> GetStoredProcedureDefinitionAsync(
            ConnectionConfig config,
            string schemaName,
            string procedureName) =>
            throw new NotSupportedException();

        public Task<IResultModel<bool>> UpdateTableCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string? comment) =>
            throw new NotSupportedException();

        public Task<IResultModel<bool>> UpdateColumnCommentAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName,
            string columnName,
            string? comment) =>
            throw new NotSupportedException();

        public Task<IResultModel<DatabaseSqlExecutionResult>> ExecuteSqlAsync(
            ConnectionConfig config,
            string sql) =>
            throw new NotSupportedException();

        private static IResultModel<List<T>> Success<T>(List<T> data, string message)
        {
            return ResultModel<List<T>>.Success(data);
        }
    }
}
