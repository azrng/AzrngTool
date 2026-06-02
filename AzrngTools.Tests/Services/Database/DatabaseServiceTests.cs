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

        Assert.False(result.Success);
        Assert.Contains("不支持的数据库类型", result.Message);
    }
}
