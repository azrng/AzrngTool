using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseServiceTests
{
    [Fact]
    public void Catalog_connection_for_postgresql_uses_postgres_database_without_mutating_source()
    {
        var service = new DatabaseService();
        var source = new ConnectionConfig
        {
            Name = "test",
            DatabaseType = DatabaseType.PostgresSql,
            Host = "127.0.0.1",
            Port = 5432,
            Username = "zhangyunpeng",
            Password = "secret",
            Database = "zhangyunpeng"
        };

        var catalogConfig = DatabaseService.CreateCatalogConnectionConfig(source);

        Assert.Equal("postgres", catalogConfig.Database);
        Assert.Equal("zhangyunpeng", source.Database);
    }

    [Fact]
    public async Task Unsupported_database_type_returns_not_supported_message()
    {
        var service = new DatabaseService();
        var config = new ConnectionConfig
        {
            Name = "dm",
            DatabaseType = DatabaseType.Dm,
            Host = "127.0.0.1",
            Port = 5236,
            Username = "user",
            Password = "pwd",
            Database = "test"
        };

        var result = await service.GetDatabaseNamesAsync(config);

        Assert.False(result.IsSuccess);
        Assert.Contains("不支持的数据库类型", result.Message);
    }

    [Fact]
    public async Task MySql_schema_uses_runtime_database_name_without_bridge_call()
    {
        var service = new DatabaseService();
        var config = new ConnectionConfig
        {
            Name = "mysql",
            DatabaseType = DatabaseType.MySql,
            Host = "127.0.0.1",
            Port = 3306,
            Username = "user",
            Password = "pwd",
            Database = "app_db"
        };

        var result = await service.GetSchemasAsync(config);

        Assert.True(result.IsSuccess);
        var schema = Assert.Single(result.Data ?? []);
        Assert.Equal("app_db", schema.Name);
        Assert.Equal("MySql", schema.Owner);
        Assert.True(schema.IsDefault);
    }

    [Fact]
    public async Task MySql_schema_falls_back_to_default_when_database_is_empty()
    {
        var service = new DatabaseService();
        var config = new ConnectionConfig
        {
            Name = "mysql",
            DatabaseType = DatabaseType.MySql,
            Host = "127.0.0.1",
            Port = 3306,
            Username = "user",
            Password = "pwd",
            Database = string.Empty
        };

        var result = await service.GetSchemasAsync(config);

        Assert.True(result.IsSuccess);
        var schema = Assert.Single(result.Data ?? []);
        Assert.Equal("default", schema.Name);
    }

    [Fact]
    public void BuildConnectionKey_is_equal_for_equivalent_connection_copies()
    {
        // 运行时连接每次都是新副本（CreateRuntimeConnection），缓存命中必须基于等值比较而非引用相等。
        var original = CreatePostgresConnection("app_db", "secret");
        var runtimeCopy = CreatePostgresConnection("app_db", "secret");

        var keyA = DatabaseService.BuildConnectionKey(DatabaseType.PostgresSql, original);
        var keyB = DatabaseService.BuildConnectionKey(DatabaseType.PostgresSql, runtimeCopy);

        Assert.NotSame(original, runtimeCopy);
        Assert.Equal(keyA, keyB);
    }

    [Theory]
    [InlineData("Host", "other-host")]
    [InlineData("Port", "5433")]
    [InlineData("Database", "other_db")]
    [InlineData("Username", "other_user")]
    [InlineData("Password", "other_pwd")]
    public void BuildConnectionKey_differs_when_connection_identity_changes(string field, string value)
    {
        var baseline = CreatePostgresConnection("app_db", "secret");
        var modified = CreatePostgresConnection("app_db", "secret");
        switch (field)
        {
            case "Host": modified.Host = value; break;
            case "Port": modified.Port = int.Parse(value); break;
            case "Database": modified.Database = value; break;
            case "Username": modified.Username = value; break;
            case "Password": modified.Password = value; break;
        }

        var baselineKey = DatabaseService.BuildConnectionKey(DatabaseType.PostgresSql, baseline);
        var modifiedKey = DatabaseService.BuildConnectionKey(DatabaseType.PostgresSql, modified);

        Assert.NotEqual(baselineKey, modifiedKey);
    }

    [Fact]
    public void TryBuildMySqlModifyColumnCommentSql_rejects_enum_type_without_fallback_to_text()
    {
        var service = new DatabaseService();
        var columnDefinition = new DatabaseService.MySqlColumnDefinitionRow
        {
            ColumnName = "status",
            ColumnType = "enum('new','done')",
            IsNullable = "NO"
        };

        var built = service.TryBuildMySqlModifyColumnCommentSql(
            "app_db",
            "orders",
            columnDefinition,
            "状态",
            out var sql,
            out var failureMessage);

        Assert.False(built);
        Assert.Empty(sql);
        Assert.Contains("暂不支持安全重建", failureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("TEXT", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ConnectionConfig CreatePostgresConnection(string database, string password)
    {
        return new ConnectionConfig
        {
            Name = "test",
            DatabaseType = DatabaseType.PostgresSql,
            Host = "127.0.0.1",
            Port = 5432,
            Username = "user",
            Password = password,
            Database = database
        };
    }
}
