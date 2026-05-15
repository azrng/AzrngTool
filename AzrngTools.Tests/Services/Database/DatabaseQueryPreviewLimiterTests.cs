using Azrng.Core.Model;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseQueryPreviewLimiterTests
{
    [Fact]
    public void BuildPreviewQuery_wraps_sqlite_select_with_limit()
    {
        var result = DatabaseQueryPreviewLimiter.BuildPreviewQuery(
            DatabaseType.Sqlite,
            "select id, name from users order by id",
            1000);

        Assert.True(result.WasLimited);
        Assert.Contains("LIMIT 1000", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id, name from users order by id", result.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPreviewQuery_injects_sql_server_top_for_simple_select()
    {
        var result = DatabaseQueryPreviewLimiter.BuildPreviewQuery(
            DatabaseType.SqlServer,
            "select id, name from users",
            500);

        Assert.True(result.WasLimited);
        Assert.Equal("SELECT TOP (500) id, name from users", result.Sql);
    }

    [Fact]
    public void BuildPreviewQuery_does_not_rewrite_query_with_existing_limit()
    {
        const string sql = "select * from users limit 20";

        var result = DatabaseQueryPreviewLimiter.BuildPreviewQuery(DatabaseType.MySql, sql, 1000);

        Assert.False(result.WasLimited);
        Assert.Equal(sql, result.Sql);
    }

    [Fact]
    public void BuildPreviewQuery_does_not_rewrite_non_query_statement()
    {
        const string sql = "update users set name = 'demo'";

        var result = DatabaseQueryPreviewLimiter.BuildPreviewQuery(DatabaseType.PostgresSql, sql, 1000);

        Assert.False(result.WasLimited);
        Assert.Equal(sql, result.Sql);
    }
}
