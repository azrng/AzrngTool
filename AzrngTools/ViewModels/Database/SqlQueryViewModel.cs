using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.ViewModels.Database;

public partial class SqlQueryViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// 危险 SQL 语句的二次确认回调。
    /// 参数为检测到的危险动作描述，返回 true 表示用户确认继续执行。
    /// 由宿主（MainWindowViewModel）注入 UI 弹窗实现，避免 ViewModel 直接依赖 Window。
    /// </summary>
    public Func<string, Task<bool>>? ConfirmDangerousSqlAsync { get; set; }

    [ObservableProperty]
    private ConnectionConfig? _currentConnection;

    [ObservableProperty]
    private string? _currentSchemaName;

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _resultColumns = new();

    [ObservableProperty]
    private ObservableCollection<string[]> _resultRows = new();

    [ObservableProperty]
    private ObservableCollection<string> _queryHistory = new();

    [ObservableProperty]
    private ObservableCollection<SqlTemplateItem> _templates = new();

    [ObservableProperty]
    private SqlTemplateItem? _selectedTemplate;

    [ObservableProperty]
    private string? _selectedHistoryItem;

    [ObservableProperty]
    private string? _resultMessage;

    [ObservableProperty]
    private int _affectedRows;

    public bool HasResults => ResultColumns.Count > 0 || ResultRows.Count > 0 || AffectedRows > 0;

    public SqlQueryViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        Templates = new ObservableCollection<SqlTemplateItem>
        {
            new("Select All", "SELECT * FROM table_name;"),
            new("Count Rows", "SELECT COUNT(1) AS total_count FROM table_name;"),
            new("Filter By Id", "SELECT * FROM table_name WHERE id = 1;"),
            new("Insert Sample", "INSERT INTO table_name (column1, column2) VALUES ('value1', 'value2');"),
            new("Update Sample", "UPDATE table_name SET column1 = 'value' WHERE id = 1;"),
            new("Delete Sample", "DELETE FROM table_name WHERE id = 1;")
        };
    }

    [RelayCommand]
    private async Task ExecuteQueryAsync()
    {
        if (CurrentConnection == null)
        {
            ToastService.ShowWarning("Please select a database connection first.", 2000);
            return;
        }

        if (string.IsNullOrWhiteSpace(SqlText))
        {
            ToastService.ShowWarning("Please enter SQL to execute.", 2000);
            return;
        }

        // 对 DROP / TRUNCATE / DELETE 等不可逆语句做二次确认，避免误操作破坏数据
        if (TryDescribeDangerousStatement(SqlText, out var dangerDescription))
        {
            var confirmed = ConfirmDangerousSqlAsync == null
                ? false
                : await ConfirmDangerousSqlAsync(dangerDescription);
            if (!confirmed)
            {
                ToastService.ShowInfo("已取消执行危险语句。", 2000);
                return;
            }
        }

        IsLoading = true;
        LoadingText = "Executing SQL...";
        ResultMessage = null;
        AffectedRows = 0;

        try
        {
            var result = await _databaseService.ExecuteSqlAsync(CurrentConnection, SqlText);
            if (!result.IsSuccess || result.Data == null)
            {
                ResultColumns.Clear();
                ResultRows.Clear();
                ResultMessage = result.Message;
                OnPropertyChanged(nameof(HasResults));
                ToastService.ShowError(result.Message, 5000);
                return;
            }

            ResultColumns.Clear();
            foreach (var col in result.Data.Columns) ResultColumns.Add(col);
            ResultRows.Clear();
            foreach (var row in result.Data.Rows) ResultRows.Add(row.ToArray());
            AffectedRows = result.Data.AffectedRows;
            ResultMessage = result.Message;
            AddToHistory(SqlText);

            OnPropertyChanged(nameof(HasResults));
            ToastService.ShowSuccess(result.Data.HasResultSet ? result.Message : $"SQL executed. {result.Data.AffectedRows} rows affected.", 3000);
        }
        catch (Exception ex)
        {
            ResultMessage = $"SQL execution failed: {ex.Message}";
            OnPropertyChanged(nameof(HasResults));
            LoggingService.LogError("ExecuteQueryAsync failed.", ex);
            ToastService.ShowError(ResultMessage, 6000);
        }
        finally
        {
            IsLoading = false;
            LoadingText = null;
        }
    }

    [RelayCommand]
    private void ApplyTemplate(SqlTemplateItem? template)
    {
        if (template == null)
        {
            return;
        }

        SqlText = ReplaceTemplatePlaceholders(template.Sql);
    }

    [RelayCommand]
    private void LoadHistory(string? historySql)
    {
        if (string.IsNullOrWhiteSpace(historySql))
        {
            return;
        }

        SqlText = historySql;
    }

    [RelayCommand]
    private void ClearResults()
    {
        ResultColumns.Clear();
        ResultRows.Clear();
        AffectedRows = 0;
        ResultMessage = null;
        OnPropertyChanged(nameof(HasResults));
    }

    private string ReplaceTemplatePlaceholders(string sql)
    {
        var schemaValue = string.IsNullOrWhiteSpace(CurrentSchemaName) ? "dbo" : CurrentSchemaName;
        return sql.Replace("{schema}", schemaValue, StringComparison.OrdinalIgnoreCase);
    }

    private void AddToHistory(string sql)
    {
        var normalizedSql = sql.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSql))
        {
            return;
        }

        var existingItem = QueryHistory.FirstOrDefault(item => string.Equals(item, normalizedSql, StringComparison.Ordinal));
        if (existingItem != null)
        {
            QueryHistory.Remove(existingItem);
        }

        QueryHistory.Insert(0, normalizedSql);

        while (QueryHistory.Count > 15)
        {
            QueryHistory.RemoveAt(QueryHistory.Count - 1);
        }
    }

    /// <summary>
    /// 检测 SQL 是否包含不可逆的危险动作（DROP / TRUNCATE / DELETE 等）。
    /// 通过分号拆分语句后逐条匹配首个有效动作，避免在含 "delete" 字样的注释或字符串里误报。
    /// </summary>
    internal static bool TryDescribeDangerousStatement(string sql, out string description)
    {
        const string defaultDescription = "即将执行不可逆的数据破坏操作。";
        description = defaultDescription;

        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var dangerKinds = new List<string>(capacity: statements.Length);

        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            var firstWord = GetDangerousActionToken(statement);

            switch (firstWord)
            {
                case "drop":
                    dangerKinds.Add("删除对象（DROP）");
                    break;
                case "truncate":
                    dangerKinds.Add("清空表数据（TRUNCATE）");
                    break;
                case "delete":
                    dangerKinds.Add("删除数据（DELETE）");
                    break;
            }
        }

        if (dangerKinds.Count == 0)
        {
            return false;
        }

        description = dangerKinds.Count == 1
            ? $"检测到不可逆操作：{dangerKinds[0]}，确定要继续执行吗？"
            : $"检测到 {dangerKinds.Count} 处不可逆操作（{string.Join("、", dangerKinds.Distinct())}），确定要继续执行吗？";
        return true;
    }

    private static string? GetDangerousActionToken(string statement)
    {
        var normalized = StripLeadingSqlComments(statement).TrimStart();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var firstWord = ReadFirstWord(normalized);
        if (!string.Equals(firstWord, "with", StringComparison.OrdinalIgnoreCase))
        {
            return firstWord.ToLowerInvariant();
        }

        var cteAction = TryReadActionAfterCommonTableExpression(normalized);
        return cteAction?.ToLowerInvariant();
    }

    private static string StripLeadingSqlComments(string sql)
    {
        var remaining = sql.TrimStart();
        while (remaining.StartsWith("--", StringComparison.Ordinal) ||
               remaining.StartsWith("/*", StringComparison.Ordinal))
        {
            if (remaining.StartsWith("--", StringComparison.Ordinal))
            {
                var lineEnd = remaining.IndexOfAny(['\r', '\n']);
                if (lineEnd < 0)
                {
                    return string.Empty;
                }

                remaining = remaining[lineEnd..].TrimStart();
                continue;
            }

            var blockEnd = remaining.IndexOf("*/", StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                return string.Empty;
            }

            remaining = remaining[(blockEnd + 2)..].TrimStart();
        }

        return remaining;
    }

    private static string ReadFirstWord(string sql)
    {
        return sql.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string? TryReadActionAfterCommonTableExpression(string sql)
    {
        var depth = 0;
        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0 && i > 0 && char.IsWhiteSpace(ch))
            {
                var rest = sql[i..].TrimStart();
                var word = ReadFirstWord(rest);
                if (word is "delete" or "DELETE" or "drop" or "DROP" or "truncate" or "TRUNCATE")
                {
                    return word;
                }
            }
        }

        return null;
    }
}

public sealed class SqlTemplateItem
{
    public SqlTemplateItem(string name, string sql)
    {
        Name = name;
        Sql = sql;
    }

    public string Name { get; }

    public string Sql { get; }
}
