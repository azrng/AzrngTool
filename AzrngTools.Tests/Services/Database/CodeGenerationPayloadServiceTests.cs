using Azrng.Core.Model;
using Azrng.Core.Results;
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

        public Task<IResultModel<List<ColumnModel>>> GetColumnsAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName)
        {
            if (FailColumns)
            {
                return Task.FromResult(Failure<List<ColumnModel>>("columns failed"));
            }

            return Task.FromResult(Success(
                new List<ColumnModel>
                {
                    new() { Name = "name", OrdinalPosition = 2 },
                    new() { Name = "id", OrdinalPosition = 1 }
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

        public Task<IResultModel<List<DatabaseViewModel>>> GetViewsAsync(ConnectionConfig config, string schemaName) =>
            throw new NotSupportedException();

        public Task<IResultModel<List<StoredProcedureModel>>> GetStoredProceduresAsync(
            ConnectionConfig config,
            string schemaName) =>
            throw new NotSupportedException();

        public Task<IResultModel<List<StoredProcedureModel>>> GetFunctionsAsync(
            ConnectionConfig config,
            string schemaName) =>
            throw new NotSupportedException();

        public Task<IResultModel<List<IndexModel>>> GetIndexesAsync(
            ConnectionConfig config,
            string schemaName,
            string tableName) =>
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

        private static IResultModel<T> Failure<T>(string message)
        {
            return ResultModel<T>.Failure(message, "TEST_FAILURE");
        }
    }
}
