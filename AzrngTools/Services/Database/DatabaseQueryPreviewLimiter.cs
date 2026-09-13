using System.Text.RegularExpressions;
using Azrng.Core.Model;

namespace AzrngTools.Services.Database;

public static class DatabaseQueryPreviewLimiter
{
    public const int DefaultMaxRows = 500;

    private static readonly Regex SqlServerSimpleSelectRegex = new(
        @"^\s*select\s+(distinct\s+)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExistingLimitTokenRegex = new(
        @"\b(limit|top|fetch|rownum)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DatabaseQueryPreviewResult BuildPreviewQuery(DatabaseType databaseType, string sql, int maxRows)
    {
        if (maxRows <= 0 || string.IsNullOrWhiteSpace(sql))
        {
            return new DatabaseQueryPreviewResult(sql, false);
        }

        var trimmedSql = sql.Trim();
        if (!LooksLikeLimitableQuery(trimmedSql) || HasExistingLimit(trimmedSql))
        {
            return new DatabaseQueryPreviewResult(sql, false);
        }

        var sqlWithoutTerminator = trimmedSql.TrimEnd(';').TrimEnd();
        return databaseType switch
        {
            DatabaseType.SqlServer => BuildSqlServerPreview(sqlWithoutTerminator, maxRows, sql),
            DatabaseType.Oracle => new DatabaseQueryPreviewResult(
                $"SELECT * FROM ({sqlWithoutTerminator}) azrng_preview WHERE ROWNUM <= {maxRows}",
                true),
            DatabaseType.MySql or DatabaseType.PostgresSql or DatabaseType.Sqlite => new DatabaseQueryPreviewResult(
                $"SELECT * FROM ({sqlWithoutTerminator}) AS azrng_preview LIMIT {maxRows}",
                true),
            _ => new DatabaseQueryPreviewResult(sql, false)
        };
    }

    private static DatabaseQueryPreviewResult BuildSqlServerPreview(string trimmedSql, int maxRows, string originalSql)
    {
        if (!trimmedSql.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
            trimmedSql.StartsWith("select distinct", StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseQueryPreviewResult(originalSql, false);
        }

        var limitedSql = SqlServerSimpleSelectRegex.Replace(trimmedSql, $"SELECT TOP ({maxRows}) ", 1);
        return new DatabaseQueryPreviewResult(limitedSql, true);
    }

    private static bool LooksLikeLimitableQuery(string sql)
    {
        return sql.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
               sql.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExistingLimit(string sql)
    {
        return ExistingLimitTokenRegex.IsMatch(sql);
    }
}

public sealed record DatabaseQueryPreviewResult(string Sql, bool WasLimited);
