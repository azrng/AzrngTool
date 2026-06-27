using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class SqlQueryViewModelTests
{
    [Theory]
    [InlineData("DROP TABLE users", "DROP", true)]
    [InlineData("Truncate Table logs", "TRUNCATE", true)]
    [InlineData("delete from orders where id = 1", "DELETE", true)]
    public void TryDescribeDangerousStatement_detects_irreversible_keywords(string sql, string expectedKeyword, bool expected)
    {
        var detected = SqlQueryViewModel.TryDescribeDangerousStatement(sql, out var description);

        Assert.Equal(expected, detected);
        Assert.Contains(expectedKeyword, description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDescribeDangerousStatement_detects_multiple_dangerous_statements()
    {
        var sql = "DELETE FROM a; DROP TABLE b; TRUNCATE TABLE c";

        var detected = SqlQueryViewModel.TryDescribeDangerousStatement(sql, out var description);

        Assert.True(detected);
        Assert.Contains("3 处", description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("UPDATE users SET name = 'a' WHERE id = 1")]
    [InlineData("INSERT INTO users (id) VALUES (1)")]
    [InlineData("CREATE TABLE t (id int)")]
    [InlineData("")]
    public void TryDescribeDangerousStatement_ignores_safe_statements(string sql)
    {
        var detected = SqlQueryViewModel.TryDescribeDangerousStatement(sql, out _);

        Assert.False(detected);
    }

    [Fact]
    public void TryDescribeDangerousStatement_ignores_delete_inside_select_subquery()
    {
        // delete 出现在列别名/字符串里，但首关键字是 SELECT，不应误报
        var sql = "SELECT 'please do not delete' AS remark FROM dual";

        var detected = SqlQueryViewModel.TryDescribeDangerousStatement(sql, out _);

        Assert.False(detected);
    }

    [Theory]
    [InlineData("-- review note\r\nDELETE FROM users WHERE id = 1", "DELETE")]
    [InlineData("/* review note */ DROP TABLE users", "DROP")]
    [InlineData("WITH target AS (SELECT id FROM users) DELETE FROM users WHERE id IN (SELECT id FROM target)", "DELETE")]
    public void TryDescribeDangerousStatement_detects_dangerous_statement_after_comments_or_cte(
        string sql,
        string expectedKeyword)
    {
        var detected = SqlQueryViewModel.TryDescribeDangerousStatement(sql, out var description);

        Assert.True(detected);
        Assert.Contains(expectedKeyword, description, StringComparison.OrdinalIgnoreCase);
    }
}
