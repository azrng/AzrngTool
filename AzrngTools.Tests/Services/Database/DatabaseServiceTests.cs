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
}
