using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartSQL.UI.Models;
using SmartSQL.UI.Services;

namespace SmartSQL.UI.ViewModels;

public partial class TableDetailViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService = new();

    public bool HasSelectedTable => SelectedTable != null;

    [ObservableProperty]
    private TableModel? _selectedTable;

    private ObservableCollection<ColumnModel> _columns = new();
    public ObservableCollection<ColumnModel> Columns
    {
        get => _columns;
        set => SetProperty(ref _columns, value);
    }

    private ObservableCollection<IndexModel> _indexes = new();
    public ObservableCollection<IndexModel> Indexes
    {
        get => _indexes;
        set => SetProperty(ref _indexes, value);
    }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasColumns;

    [ObservableProperty]
    private bool _hasIndexes;

    [ObservableProperty]
    private string? _loadingText;

    [ObservableProperty]
    private string? _tableComment;

    [ObservableProperty]
    private string? _schemaName;

    [ObservableProperty]
    private long _rowCount;

    [ObservableProperty]
    private DateTime? _createTime;

    [ObservableProperty]
    private DateTime? _modifyTime;

    [ObservableProperty]
    private string _tableDefinition = string.Empty;

    [ObservableProperty]
    private string _columnsText = "暂无字段数据";

    [ObservableProperty]
    private string _indexesText = "暂无索引数据";

    [ObservableProperty]
    private ConnectionConfig? _currentConnection;

    [ObservableProperty]
    private ObservableCollection<TableModel> _tables = new();

    [ObservableProperty]
    private bool _showObjectList = true;

    [ObservableProperty]
    private bool _showColumnsInfoBanner = true;

    [ObservableProperty]
    private bool _showIndexesInfoBanner = true;

    [ObservableProperty]
    private bool _showDdlInfoBanner = true;

    [ObservableProperty]
    private bool _isSavingComment;

    public bool ShowDetailOnly => !ShowObjectList;

    public bool ShowEmptyState => ShowObjectList && Tables.Count == 0;

    public bool ShowSplitWorkspace => ShowObjectList && Tables.Count > 0;

    public bool ShowColumnsTab => SelectedTabIndex == 0;

    public bool ShowIndexesTab => SelectedTabIndex == 1;

    public bool ShowDdlTab => SelectedTabIndex == 2;

    public bool ShowColumnsEmptyState => HasSelectedTable && !IsLoading && !HasColumns;

    public bool ShowIndexesEmptyState => HasSelectedTable && !IsLoading && !HasIndexes;

    public bool ShowColumnsTabEmptyState => ShowColumnsTab && ShowColumnsEmptyState;

    public bool ShowIndexesTabEmptyState => ShowIndexesTab && ShowIndexesEmptyState;

    public bool CanEditComments =>
        HasSelectedTable &&
        CurrentConnection != null &&
        SupportsCommentEditing(CurrentConnection.DatabaseType) &&
        !IsLoading &&
        !IsSavingComment;

    public string TableCommentDisplayText => string.IsNullOrWhiteSpace(TableComment)
        ? "暂无备注"
        : TableComment!;

    public TableDetailViewModel()
    {
        SubscribeToTablesCollection(Tables);
    }

    public async Task LoadTablesBySchemaAsync(string schemaName)
    {
        if (CurrentConnection == null || string.IsNullOrWhiteSpace(schemaName))
        {
            LoggingService.LogWarning("LoadTablesBySchemaAsync skipped because connection or schema is missing.");
            return;
        }

        IsLoading = true;
        LoadingText = $"正在加载架构 [{schemaName}] 下的数据表...";
        Tables.Clear();
        ClearSelectionState();

        try
        {
            var result = await _databaseService.GetTablesAsync(CurrentConnection, schemaName);
            if (!result.Success)
            {
                LoadingText = result.Message;
                LoggingService.LogError($"加载数据表失败：{result.Message}");
                return;
            }

            foreach (var table in result.Tables.OrderBy(table => table.Name))
            {
                Tables.Add(table);
            }

            LoadingText = $"已加载 {Tables.Count} 个数据表。";
            LoggingService.LogInfo($"Loaded {Tables.Count} tables for schema {schemaName}.");
        }
        catch (Exception ex)
        {
            LoadingText = $"加载数据表失败：{ex.Message}";
            LoggingService.LogError("LoadTablesBySchemaAsync failed.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (SelectedTable == null || CurrentConnection == null)
        {
            LoggingService.LogWarning($"LoadDataAsync skipped: SelectedTable={SelectedTable}, CurrentConnection={CurrentConnection}");
            return;
        }

        LoggingService.LogInfo($"LoadDataAsync starting for table: {SelectedTable.Schema}.{SelectedTable.Name}");

        IsLoading = true;
        LoadingText = $"正在加载数据表 {SelectedTable.Name} 的详情...";

        // 重新创建集合以确保 UI 正确更新
        Columns = new ObservableCollection<ColumnModel>();
        Indexes = new ObservableCollection<IndexModel>();
        HasColumns = false;
        HasIndexes = false;

        try
        {
            var schemaName = string.IsNullOrWhiteSpace(SelectedTable.Schema) ? "dbo" : SelectedTable.Schema;
            LoggingService.LogInfo($"Loading columns for {schemaName}.{SelectedTable.Name}...");

            var columnsResult = await _databaseService.GetColumnsAsync(CurrentConnection, schemaName, SelectedTable.Name);
            LoggingService.LogInfo($"GetColumnsAsync result: Success={columnsResult.Success}, Count={columnsResult.Columns?.Count ?? 0}, Message={columnsResult.Message}");

            if (columnsResult.Success)
            {
                foreach (var column in (columnsResult.Columns ?? Enumerable.Empty<ColumnModel>())
                             .OrderBy(column => column.OrdinalPosition))
                {
                    Columns.Add(column);
                }
                HasColumns = Columns.Count > 0;
                LoggingService.LogInfo($"Added {Columns.Count} columns to collection");
            }
            else
            {
                LoggingService.LogError($"GetColumnsAsync failed: {columnsResult.Message}");
            }

            LoggingService.LogInfo($"Loading indexes for {schemaName}.{SelectedTable.Name}...");
            var indexesResult = await _databaseService.GetIndexesAsync(CurrentConnection, schemaName, SelectedTable.Name);
            LoggingService.LogInfo($"GetIndexesAsync result: Success={indexesResult.Success}, Count={indexesResult.Indexes?.Count ?? 0}, Message={indexesResult.Message}");

            if (indexesResult.Success)
            {
                foreach (var index in indexesResult.Indexes ?? Enumerable.Empty<IndexModel>())
                {
                    Indexes.Add(index);
                }
                HasIndexes = Indexes.Count > 0;
                LoggingService.LogInfo($"Added {Indexes.Count} indexes to collection");
            }
            else
            {
                LoggingService.LogError($"GetIndexesAsync failed: {indexesResult.Message}");
            }

            var statsResult = await _databaseService.GetTableStatisticsAsync(CurrentConnection, schemaName, SelectedTable.Name);
            if (statsResult.Success)
            {
                RowCount = statsResult.RowCount;
                CreateTime = statsResult.CreateTime;
                ModifyTime = statsResult.ModifyTime;
            }
            else
            {
                RowCount = 0;
                CreateTime = null;
                ModifyTime = null;
            }

            TableComment = SelectedTable.Comment;
            SchemaName = SelectedTable.Schema;
            ColumnsText = BuildColumnsText(Columns);
            IndexesText = BuildIndexesText(Indexes);
            TableDefinition = BuildTableDefinitionScript(SelectedTable, Columns, Indexes);
            LoadingText = $"已加载 {Columns.Count} 个字段和 {Indexes.Count} 个索引。";
            LoggingService.LogInfo($"LoadDataAsync completed: {Columns.Count} columns, {Indexes.Count} indexes");
        }
        catch (Exception ex)
        {
            LoadingText = $"加载数据表详情失败：{ex.Message}";
            LoggingService.LogError("LoadDataAsync failed for table details.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedTable != null)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void SelectTab(string? tabKey)
    {
        SelectedTabIndex = tabKey switch
        {
            "indexes" => 1,
            "ddl" => 2,
            _ => 0
        };
    }

    [RelayCommand]
    private void Clear()
    {
        Tables.Clear();
        ClearSelectionState();
    }

    [RelayCommand]
    private void DismissColumnsBanner() => ShowColumnsInfoBanner = false;

    [RelayCommand]
    private void DismissIndexesBanner() => ShowIndexesInfoBanner = false;

    [RelayCommand]
    private void DismissDdlBanner() => ShowDdlInfoBanner = false;

    public async Task<(bool Success, string? ErrorMessage)> SaveTableCommentAsync(string newComment)
    {
        if (SelectedTable == null || CurrentConnection == null)
        {
            return (false, "未选择要编辑的数据表。");
        }

        if (!SupportsCommentEditing(CurrentConnection.DatabaseType))
        {
            return (false, "当前连接类型暂不支持修改备注。");
        }

        var normalizedComment = NormalizeComment(newComment);
        if (string.Equals(NormalizeComment(TableComment), normalizedComment, StringComparison.Ordinal))
        {
            return (false, "备注内容未发生变化。");
        }

        IsSavingComment = true;

        try
        {
            var schemaName = ResolveSchemaName(SelectedTable.Schema);
            var result = await _databaseService.UpdateTableCommentAsync(
                CurrentConnection,
                schemaName,
                SelectedTable.Name,
                normalizedComment);

            if (!result.Success)
            {
                ToastService.ShowError(result.Message, 5000);
                return (false, result.Message);
            }

            SelectedTable.Comment = normalizedComment;
            TableComment = normalizedComment;
            TableDefinition = BuildTableDefinitionScript(SelectedTable, Columns, Indexes);
            ToastService.ShowSuccess("表备注已更新。", 2500);
            return (true, null);
        }
        finally
        {
            IsSavingComment = false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SaveColumnCommentAsync(ColumnModel column, string newComment)
    {
        if (column == null || SelectedTable == null || CurrentConnection == null)
        {
            return (false, "未选择要编辑的字段。");
        }

        if (!SupportsCommentEditing(CurrentConnection.DatabaseType))
        {
            return (false, "当前连接类型暂不支持修改备注。");
        }

        var normalizedComment = NormalizeComment(newComment);
        if (string.Equals(NormalizeComment(column.Comment), normalizedComment, StringComparison.Ordinal))
        {
            return (false, "备注内容未发生变化。");
        }

        IsSavingComment = true;

        try
        {
            var schemaName = ResolveSchemaName(SelectedTable.Schema);
            var result = await _databaseService.UpdateColumnCommentAsync(
                CurrentConnection,
                schemaName,
                SelectedTable.Name,
                column.Name,
                normalizedComment);

            if (!result.Success)
            {
                ToastService.ShowError(result.Message, 5000);
                return (false, result.Message);
            }

            column.Comment = normalizedComment;
            ColumnsText = BuildColumnsText(Columns);
            TableDefinition = BuildTableDefinitionScript(SelectedTable, Columns, Indexes);
            ToastService.ShowSuccess($"字段 {column.Name} 备注已更新。", 2500);
            return (true, null);
        }
        finally
        {
            IsSavingComment = false;
        }
    }

    partial void OnSelectedTableChanged(TableModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedTable));
        OnPropertyChanged(nameof(ShowColumnsEmptyState));
        OnPropertyChanged(nameof(ShowIndexesEmptyState));
        OnPropertyChanged(nameof(ShowColumnsTabEmptyState));
        OnPropertyChanged(nameof(ShowIndexesTabEmptyState));
        OnPropertyChanged(nameof(CanEditComments));

        if (value != null)
        {
            _ = LoadDataAsync();
        }
    }

    partial void OnShowObjectListChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetailOnly));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowColumnsTab));
        OnPropertyChanged(nameof(ShowIndexesTab));
        OnPropertyChanged(nameof(ShowDdlTab));
        OnPropertyChanged(nameof(ShowColumnsTabEmptyState));
        OnPropertyChanged(nameof(ShowIndexesTabEmptyState));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowColumnsEmptyState));
        OnPropertyChanged(nameof(ShowIndexesEmptyState));
        OnPropertyChanged(nameof(ShowColumnsTabEmptyState));
        OnPropertyChanged(nameof(ShowIndexesTabEmptyState));
        OnPropertyChanged(nameof(CanEditComments));
    }

    partial void OnHasColumnsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowColumnsEmptyState));
        OnPropertyChanged(nameof(ShowColumnsTabEmptyState));
    }

    partial void OnHasIndexesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIndexesEmptyState));
        OnPropertyChanged(nameof(ShowIndexesTabEmptyState));
    }

    partial void OnTableCommentChanged(string? value)
    {
        OnPropertyChanged(nameof(TableCommentDisplayText));
    }

    partial void OnCurrentConnectionChanged(ConnectionConfig? value)
    {
        OnPropertyChanged(nameof(CanEditComments));
    }

    partial void OnIsSavingCommentChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditComments));
    }

    partial void OnTablesChanged(ObservableCollection<TableModel> value)
    {
        SubscribeToTablesCollection(value);
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    private void SubscribeToTablesCollection(ObservableCollection<TableModel> tables)
    {
        tables.CollectionChanged += OnTablesCollectionChanged;
    }

    private void OnTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    private void ClearSelectionState()
    {
        SelectedTable = null;
        Columns.Clear();
        Indexes.Clear();
        HasColumns = false;
        HasIndexes = false;
        TableComment = null;
        SchemaName = null;
        RowCount = 0;
        CreateTime = null;
        ModifyTime = null;
        ColumnsText = "暂无字段数据";
        IndexesText = "暂无索引数据";
        TableDefinition = string.Empty;
        SelectedTabIndex = 0;
    }

    private string ResolveSchemaName(string? schemaName)
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            return schemaName;
        }

        return CurrentConnection?.DatabaseType == DatabaseType.MySql
            ? CurrentConnection.Database
            : "dbo";
    }

    private static bool SupportsCommentEditing(DatabaseType databaseType)
    {
        return databaseType is DatabaseType.PostgresSql or DatabaseType.MySql;
    }

    private static string NormalizeComment(string? comment) => comment?.Trim() ?? string.Empty;

    private static string BuildColumnsText(ObservableCollection<ColumnModel> columns)
    {
        if (columns.Count == 0)
        {
            return "暂无字段数据";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"字段总数：{columns.Count}");
        builder.AppendLine("----------------------------------------");

        foreach (var column in columns.OrderBy(column => column.OrdinalPosition))
        {
            builder.AppendLine($"[{column.OrdinalPosition}] {column.Name}");
            builder.AppendLine($"  类型：{column.DataType}");
            builder.AppendLine($"  长度：{(column.Length > 0 ? column.Length : "-")}");
            builder.AppendLine($"  可空：{ToYesNo(column.IsNullable)}    主键：{ToYesNo(column.IsPrimaryKey)}    标识：{ToYesNo(column.IsIdentity)}");
            builder.AppendLine($"  默认值：{FormatText(column.DefaultValue)}");
            builder.AppendLine($"  备注：{FormatText(column.Comment)}");
            builder.AppendLine("----------------------------------------");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildIndexesText(ObservableCollection<IndexModel> indexes)
    {
        if (indexes.Count == 0)
        {
            return "暂无索引数据";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"索引总数：{indexes.Count}");
        builder.AppendLine("----------------------------------------");

        foreach (var index in indexes)
        {
            builder.AppendLine(index.Name);
            builder.AppendLine($"  类型：{index.IndexType}");
            builder.AppendLine($"  唯一：{ToYesNo(index.IsUnique)}    主键：{ToYesNo(index.IsPrimaryKey)}    升序：{ToYesNo(index.IsAscending)}");
            builder.AppendLine($"  字段：{FormatText(index.Columns)}");
            builder.AppendLine("----------------------------------------");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ToYesNo(bool value) => value ? "是" : "否";

    private static string FormatText(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static string BuildTableDefinitionScript(
        TableModel table,
        ObservableCollection<ColumnModel> columns,
        ObservableCollection<IndexModel> indexes)
    {
        var builder = new StringBuilder();
        var qualifiedTableName = string.IsNullOrWhiteSpace(table.Schema)
            ? table.Name
            : $"{table.Schema}.{table.Name}";

        builder.AppendLine($"CREATE TABLE {qualifiedTableName}");
        builder.AppendLine("(");

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var lineBuilder = new StringBuilder();
            lineBuilder.Append("    ");
            lineBuilder.Append(column.Name);
            lineBuilder.Append(' ');
            lineBuilder.Append(column.DataType);

            if (column.Length > 0 &&
                !column.DataType.Contains("text", StringComparison.OrdinalIgnoreCase) &&
                !column.DataType.Contains("date", StringComparison.OrdinalIgnoreCase) &&
                !column.DataType.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                lineBuilder.Append('(');
                lineBuilder.Append(column.Length);
                lineBuilder.Append(')');
            }

            if (!column.IsNullable)
            {
                lineBuilder.Append(" NOT NULL");
            }

            if (!string.IsNullOrWhiteSpace(column.DefaultValue))
            {
                lineBuilder.Append(" DEFAULT ");
                lineBuilder.Append(column.DefaultValue);
            }

            if (column.IsIdentity)
            {
                lineBuilder.Append(" IDENTITY");
            }

            if (index < columns.Count - 1 || columns.Any(col => col.IsPrimaryKey))
            {
                lineBuilder.Append(',');
            }

            builder.AppendLine(lineBuilder.ToString());
        }

        var primaryKeyColumns = columns
            .Where(column => column.IsPrimaryKey)
            .OrderBy(column => column.OrdinalPosition)
            .Select(column => column.Name)
            .ToList();

        if (primaryKeyColumns.Count > 0)
        {
            builder.AppendLine($"    PRIMARY KEY ({string.Join(", ", primaryKeyColumns)})");
        }

        builder.AppendLine(");");

        if (!string.IsNullOrWhiteSpace(table.Comment))
        {
            builder.AppendLine();
            builder.AppendLine($"-- Comment: {table.Comment}");
        }

        if (indexes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("-- Indexes");

            foreach (var index in indexes)
            {
                var uniqueText = index.IsUnique ? "UNIQUE " : string.Empty;
                builder.AppendLine($"CREATE {uniqueText}INDEX {index.Name} ON {qualifiedTableName} ({index.Columns});");
            }
        }

        return builder.ToString();
    }
}
