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
    private readonly DatabaseService _databaseService = new();

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

    public SqlQueryViewModel()
    {
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

        IsLoading = true;
        LoadingText = "Executing SQL...";
        ResultMessage = null;
        AffectedRows = 0;

        try
        {
            var (success, hasResultSet, columns, rows, affectedRows, message) = await _databaseService.ExecuteSqlAsync(CurrentConnection, SqlText);
            if (!success)
            {
                ResultColumns.Clear();
                ResultRows.Clear();
                ResultMessage = message;
                OnPropertyChanged(nameof(HasResults));
                ToastService.ShowError(message, 5000);
                return;
            }

            ResultColumns.Clear();
            foreach (var col in columns) ResultColumns.Add(col);
            ResultRows.Clear();
            foreach (var row in rows) ResultRows.Add(row.ToArray());
            AffectedRows = affectedRows;
            ResultMessage = message;
            AddToHistory(SqlText);

            OnPropertyChanged(nameof(HasResults));
            ToastService.ShowSuccess(hasResultSet ? message : $"SQL executed. {affectedRows} rows affected.", 3000);
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
