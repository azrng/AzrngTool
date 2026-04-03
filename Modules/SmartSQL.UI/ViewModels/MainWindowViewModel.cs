using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartSQL.UI.Models;
using SmartSQL.UI.Models.DTOs;
using SmartSQL.UI.Services;
using SmartSQL.UI.Views;

namespace SmartSQL.UI.ViewModels;

public enum DetailWorkspaceMode
{
    Schema,
    Table,
    View,
    Procedure
}

public partial class MainWindowViewModel : ViewModelBase
{
    private const string ConfigFileName = "connections.json";
    private const string GroupsFileName = "groups.json";

    private readonly string _configFilePath;
    private readonly string _groupsFilePath;
    private readonly DatabaseService _databaseService = new();
    private readonly DocumentExportService _documentExportService = new();
    private readonly CodeGenerationService _codeGenerationService = new();
    private bool _suppressDatabaseSelectionChanged;
    private string? _lastDocumentExportDirectory;

    [ObservableProperty]
    private ObservableCollection<ConnectionConfig> _connections = new();

    [ObservableProperty]
    private ConnectionConfig? _selectedConnection;

    [ObservableProperty]
    private ConnectionConfig? _activeConnectionContext;

    [ObservableProperty]
    private ObservableCollection<string> _availableDatabases = new();

    [ObservableProperty]
    private string? _selectedDatabaseName;

    [ObservableProperty]
    private ObservableCollection<ConnectionGroup> _groups = new();

    [ObservableProperty]
    private string? _selectedGroupId;

    [ObservableProperty]
    private string _sortMode = "Name";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadingText;

    [ObservableProperty]
    private ObservableCollection<SchemaModel> _schemas = new();

    [ObservableProperty]
    private DatabaseBrowserViewModel _browserViewModel = new();

    [ObservableProperty]
    private TableDetailViewModel _tableDetailViewModel = new();

    [ObservableProperty]
    private ViewDetailViewModel _viewDetailViewModel = new();

    [ObservableProperty]
    private StoredProcedureDetailViewModel _storedProcedureDetailViewModel = new();

    [ObservableProperty]
    private SqlQueryViewModel _sqlQueryViewModel = new();

    [ObservableProperty]
    private int _selectedMainTabIndex;

    [ObservableProperty]
    private string? _currentSchemaName;

    [ObservableProperty]
    private SchemaModel? _currentSchema;

    [ObservableProperty]
    private bool _showOverviewPage = true;

    [ObservableProperty]
    private DetailWorkspaceMode _currentWorkspaceMode = DetailWorkspaceMode.Table;

    public bool ShowSchemaWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.Schema;

    public bool ShowTableWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.Table;

    public bool ShowViewWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.View;

    public bool ShowProcedureWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.Procedure;

    public bool HasAvailableDatabases => AvailableDatabases.Count > 0;

    public string CurrentConnectionLabel => SelectedConnection == null
        ? "连接: 未选择连接"
        : string.IsNullOrWhiteSpace(SelectedDatabaseName)
            ? $"连接: {SelectedConnection.Name}"
            : $"连接: {SelectedConnection.Name} / 数据库: {SelectedDatabaseName}";

    public string CompactConnectionLabel => SelectedConnection == null
        ? "未连接"
        : string.IsNullOrWhiteSpace(SelectedDatabaseName)
            ? SelectedConnection.Name
            : $"{SelectedConnection.Name} / {SelectedDatabaseName}";

    public string Greeting { get; } = $"AzrngTools Database Workbench v{GetAppVersion()}";

    public Window? MainWindow { get; set; }

    public MainWindowViewModel()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartDbSql");
        Directory.CreateDirectory(appDataDir);
        _configFilePath = Path.Combine(appDataDir, ConfigFileName);
        _groupsFilePath = Path.Combine(appDataDir, GroupsFileName);
        AvailableDatabases.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAvailableDatabases));

        LoadGroups();
        LoadConnections();
    }

    private static string GetAppVersion()
    {
        return typeof(global::AzrngTools.App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private void LoadGroups()
    {
        try
        {
            if (!File.Exists(_groupsFilePath))
            {
                CreateDefaultGroup();
                return;
            }

            var options = CreateJsonOptions();
            var groups = JsonSerializer.Deserialize<List<ConnectionGroup>>(File.ReadAllText(_groupsFilePath), options);
            if (groups == null || groups.Count == 0)
            {
                CreateDefaultGroup();
                return;
            }

            Groups = new ObservableCollection<ConnectionGroup>(groups);
            LoggingService.LogInfo($"Loaded {groups.Count} connection groups.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to load connection groups.", ex);
            CreateDefaultGroup();
        }
    }

    private void CreateDefaultGroup()
    {
        Groups = new ObservableCollection<ConnectionGroup>
        {
            new()
            {
                Id = "default",
                Name = "Default",
                Description = "Default connection group",
                Color = "#E3EFE8",
                IsDefault = true
            }
        };

        SaveGroups();
    }

    private void SaveGroups()
    {
        try
        {
            var json = JsonSerializer.Serialize(Groups.ToList(), CreateJsonOptions());
            File.WriteAllText(_groupsFilePath, json);
            LoggingService.LogOperation($"Saved {Groups.Count} connection groups.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to save connection groups.", ex);
        }
    }

    private void LoadConnections()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                Connections = new ObservableCollection<ConnectionConfig>();
                LoggingService.LogInfo("Connection config file does not exist yet.");
                return;
            }

            var options = CreateJsonOptions();
            var connections = JsonSerializer.Deserialize<List<ConnectionConfig>>(File.ReadAllText(_configFilePath), options)
                ?? new List<ConnectionConfig>();

            foreach (var connection in connections)
            {
                DecryptConnectionPassword(connection);
            }

            Connections = new ObservableCollection<ConnectionConfig>(SortConnections(connections));
            LoggingService.LogInfo($"Loaded {Connections.Count} connections.");
        }
        catch (Exception ex)
        {
            Connections = new ObservableCollection<ConnectionConfig>();
            LoggingService.LogError("Failed to load connection configuration.", ex);
        }
    }

    private List<ConnectionConfig> SortConnections(IEnumerable<ConnectionConfig> connections)
    {
        return SortMode switch
        {
            "Name" => connections.OrderBy(connection => connection.Name).ToList(),
            "Type" => connections.OrderBy(connection => connection.DatabaseType).ToList(),
            "LastUsed" => connections.OrderByDescending(connection => connection.LastUsedTime ?? DateTime.MinValue).ToList(),
            "UsageCount" => connections.OrderByDescending(connection => connection.UseCount).ToList(),
            _ => connections.OrderBy(connection => connection.Name).ToList()
        };
    }

    private void SaveConnections()
    {
        try
        {
            EncryptPasswordsInPlace(Connections);

            var json = JsonSerializer.Serialize(Connections.ToList(), CreateJsonOptions());
            File.WriteAllText(_configFilePath, json);
            LoggingService.LogOperation($"Saved {Connections.Count} connections.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to save connection configuration.", ex);
            throw;
        }
        finally
        {
            DecryptPasswordsInPlace(Connections);
        }
    }

    [RelayCommand]
    private async Task ShowAddConnectionDialogAsync()
    {
        try
        {
            if (MainWindow == null)
            {
                LoggingService.LogWarning("MainWindow is not available.");
                return;
            }

            var dialog = new ConnectionDialog(Connections, SaveConnections, SelectedConnection)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var result = await dialog.ShowDialog<ConnectionConfig?>(MainWindow);
            if (result == null)
            {
                return;
            }

            result.UpdateUsageStats();
            SelectedConnection = result;
            LoggingService.LogOperation($"Selected connection: {result.Name}");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to open the connection dialog.", ex);
            await ShowErrorMessageAsync("打开连接管理失败", $"无法打开连接管理对话框。\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void AddConnection(ConnectionConfig connection)
    {
        if (connection == null)
        {
            return;
        }

        Connections.Add(connection);
        SaveConnections();
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection == null)
        {
            return;
        }

        if (MainWindow == null)
        {
            LoggingService.LogWarning("MainWindow is not available.");
            return;
        }

        try
        {
            var messageBox = new MessageBox
            {
                Title = "删除连接",
                Message = $"确定删除连接“{SelectedConnection.Name}”？\n\n此操作无法撤销。",
                Buttons = MessageBoxButtons.YesNo,
                DefaultButton = MessageBoxButtonType.No
            };

            var result = await messageBox.ShowDialogAsync(MainWindow);
            if (result != MessageBoxButtonType.Yes)
            {
                return;
            }

            var deletedName = SelectedConnection.Name;
            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
            SaveConnections();

            LoggingService.LogOperation($"Deleted connection: {deletedName}");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("删除连接失败。", ex);
            await ShowErrorMessageAsync("删除连接失败", $"无法删除当前连接。\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync(ConnectionConfig? connection)
    {
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择一个连接。", 2000);
            return;
        }

        try
        {
            IsLoading = true;
            LoadingText = "正在测试连接...";

            var (success, message, suggestion) = await _databaseService.TestConnectionAsync(connection);
            if (success)
            {
                LoadingText = $"连接测试成功：{connection.Name}";
                LoggingService.LogOperation($"连接测试成功：{connection.Name}");
                await InitializeConnectionContextAsync(connection);
                ToastService.ShowSuccess($"连接成功：{connection.Name}\n{message}", 3000);
                return;
            }

            LoadingText = $"连接测试失败：{connection.Name}";
            LoggingService.LogError($"连接测试失败：{connection.Name} - {message}");

            var fullMessage = string.IsNullOrWhiteSpace(suggestion)
                ? message
                : $"{message}\n\n建议：\n{suggestion}";

            ToastService.ShowError($"连接失败：{connection.Name}\n\n{fullMessage}", 6000);
        }
        catch (Exception ex)
        {
            LoadingText = "连接测试失败。";
            LoggingService.LogError("连接测试发生异常。", ex);
            ToastService.ShowError($"连接测试失败：{ex.Message}", 6000);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedConnectionChanging(ConnectionConfig? value)
    {
        Schemas.Clear();
        CurrentSchemaName = null;
        CurrentSchema = null;
        ActiveConnectionContext = null;
        AvailableDatabases.Clear();

        _suppressDatabaseSelectionChanged = true;
        SelectedDatabaseName = null;
        _suppressDatabaseSelectionChanged = false;

        SqlQueryViewModel.CurrentConnection = null;
        ResetWorkspaceState();

        if (value == null)
        {
            ShowOverviewPage = true;
        }

        OnPropertyChanged(nameof(CurrentConnectionLabel));
        OnPropertyChanged(nameof(CompactConnectionLabel));
    }

    partial void OnSelectedConnectionChanged(ConnectionConfig? value)
    {
        OnPropertyChanged(nameof(CurrentConnectionLabel));
        OnPropertyChanged(nameof(CompactConnectionLabel));

        if (value == null)
        {
            return;
        }

        _ = InitializeConnectionContextAsync(value);
    }

    partial void OnSelectedDatabaseNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CurrentConnectionLabel));
        OnPropertyChanged(nameof(CompactConnectionLabel));

        if (_suppressDatabaseSelectionChanged || SelectedConnection == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = SwitchDatabaseAsync(value);
    }

    partial void OnShowOverviewPageChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSchemaWorkspace));
        OnPropertyChanged(nameof(ShowTableWorkspace));
        OnPropertyChanged(nameof(ShowViewWorkspace));
        OnPropertyChanged(nameof(ShowProcedureWorkspace));
    }

    partial void OnCurrentWorkspaceModeChanged(DetailWorkspaceMode value)
    {
        OnPropertyChanged(nameof(ShowSchemaWorkspace));
        OnPropertyChanged(nameof(ShowTableWorkspace));
        OnPropertyChanged(nameof(ShowViewWorkspace));
        OnPropertyChanged(nameof(ShowProcedureWorkspace));
    }

    partial void OnCurrentSchemaNameChanged(string? value)
    {
        SqlQueryViewModel.CurrentSchemaName = value;
    }

    [RelayCommand]
    private async Task RefreshBrowserAsync()
    {
        var connection = GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        BrowserViewModel.CurrentConnection = connection;
        await BrowserViewModel.LoadDataAsync();
        ToastService.ShowInfo($"已刷新对象树：{connection.Name} / {connection.Database}", 2000);
    }

    private async Task InitializeConnectionContextAsync(ConnectionConfig connection)
    {
        var preferredDatabase = string.IsNullOrWhiteSpace(connection.Database)
            ? null
            : connection.Database;

        await LoadAvailableDatabasesAsync(connection, preferredDatabase);

        if (SelectedConnection != connection)
        {
            return;
        }

        var runtimeConnection = CreateRuntimeConnection(connection, SelectedDatabaseName);
        ActiveConnectionContext = runtimeConnection;
        await LoadConnectionContextAsync(runtimeConnection);
    }

    private async Task SwitchDatabaseAsync(string databaseName)
    {
        if (SelectedConnection == null)
        {
            return;
        }

        var runtimeConnection = CreateRuntimeConnection(SelectedConnection, databaseName);
        ActiveConnectionContext = runtimeConnection;
        ResetWorkspaceState();
        await LoadConnectionContextAsync(runtimeConnection);
    }

    private async Task LoadConnectionContextAsync(ConnectionConfig connection)
    {
        ActiveConnectionContext = connection;
        SqlQueryViewModel.CurrentConnection = connection;
        TableDetailViewModel.CurrentConnection = connection;
        ViewDetailViewModel.CurrentConnection = connection;
        StoredProcedureDetailViewModel.CurrentConnection = connection;
        BrowserViewModel.CurrentConnection = connection;
        await LoadSchemasAsync(connection);
        await BrowserViewModel.LoadDataAsync();
    }

    public async Task ActivateSchemaAsync(SchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        var connection = GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        ApplySchemaContext(schema.Name);
        CurrentSchema = schema;
        TableDetailViewModel.CurrentConnection = connection;
        ViewDetailViewModel.CurrentConnection = connection;
        StoredProcedureDetailViewModel.CurrentConnection = connection;
        TableDetailViewModel.ShowObjectList = true;
        ViewDetailViewModel.ShowObjectList = true;
        StoredProcedureDetailViewModel.ShowObjectList = true;
        TableDetailViewModel.SelectedTable = null;
        ViewDetailViewModel.SelectedView = null;
        StoredProcedureDetailViewModel.SelectedProcedure = null;

        await TableDetailViewModel.LoadTablesBySchemaAsync(schema.Name);
        await ViewDetailViewModel.LoadViewsBySchemaAsync(schema.Name);
        await StoredProcedureDetailViewModel.LoadProceduresBySchemaAsync(schema.Name);

        ShowOverviewPage = false;
        CurrentWorkspaceMode = DetailWorkspaceMode.Schema;
    }

    public void ActivateWorkspaceFolder(string? nodeName)
    {
        ShowOverviewPage = false;

        CurrentWorkspaceMode = nodeName switch
        {
            "Views" => DetailWorkspaceMode.View,
            "Stored Procedures" => DetailWorkspaceMode.Procedure,
            "Functions" => DetailWorkspaceMode.Procedure,
            _ => DetailWorkspaceMode.Table
        };

        TableDetailViewModel.ShowObjectList = CurrentWorkspaceMode == DetailWorkspaceMode.Table;
        ViewDetailViewModel.ShowObjectList = CurrentWorkspaceMode != DetailWorkspaceMode.View;
        StoredProcedureDetailViewModel.ShowObjectList = CurrentWorkspaceMode != DetailWorkspaceMode.Procedure;

        if (CurrentWorkspaceMode == DetailWorkspaceMode.View)
        {
            ViewDetailViewModel.SelectedView = null;
        }

        if (CurrentWorkspaceMode == DetailWorkspaceMode.Procedure)
        {
            StoredProcedureDetailViewModel.SelectedProcedure = null;
        }
    }

    public async Task ActivateTableAsync(TableModel table)
    {
        if (table == null)
        {
            return;
        }

        var connection = GetActiveConnection();
        if (connection == null)
        {
            return;
        }

        ApplySchemaContext(table.Schema);
        TableDetailViewModel.CurrentConnection = connection;
        TableDetailViewModel.ShowObjectList = false;
        ViewDetailViewModel.ShowObjectList = true;
        StoredProcedureDetailViewModel.ShowObjectList = true;
        ViewDetailViewModel.SelectedView = null;
        StoredProcedureDetailViewModel.SelectedProcedure = null;
        ShowOverviewPage = false;

        if (!string.IsNullOrWhiteSpace(table.Schema))
        {
            var hasTableInWorkspace = TableDetailViewModel.Tables.Any(existingTable =>
                existingTable.Schema == table.Schema &&
                existingTable.Name == table.Name);

            if (!hasTableInWorkspace)
            {
                await TableDetailViewModel.LoadTablesBySchemaAsync(table.Schema);
            }
        }

        CurrentWorkspaceMode = DetailWorkspaceMode.Table;
        TableDetailViewModel.SelectedTable = TableDetailViewModel.Tables.FirstOrDefault(existingTable =>
            existingTable.Schema == table.Schema &&
            existingTable.Name == table.Name) ?? table;
    }

    public async Task ActivateViewAsync(Models.ViewModel view)
    {
        if (view == null)
        {
            return;
        }

        var connection = GetActiveConnection();
        if (connection == null)
        {
            return;
        }

        ApplySchemaContext(view.Schema);
        ViewDetailViewModel.CurrentConnection = connection;
        TableDetailViewModel.ShowObjectList = true;
        ViewDetailViewModel.ShowObjectList = false;
        StoredProcedureDetailViewModel.ShowObjectList = true;
        TableDetailViewModel.SelectedTable = null;
        StoredProcedureDetailViewModel.SelectedProcedure = null;
        ShowOverviewPage = false;

        if (!string.IsNullOrWhiteSpace(view.Schema))
        {
            var hasViewInWorkspace = ViewDetailViewModel.Views.Any(existingView =>
                existingView.Schema == view.Schema &&
                existingView.Name == view.Name);

            if (!hasViewInWorkspace)
            {
                await ViewDetailViewModel.LoadViewsBySchemaAsync(view.Schema);
            }
        }

        CurrentWorkspaceMode = DetailWorkspaceMode.View;
        ViewDetailViewModel.SelectedView = ViewDetailViewModel.Views.FirstOrDefault(existingView =>
            existingView.Schema == view.Schema &&
            existingView.Name == view.Name) ?? view;
    }

    public async Task ActivateProcedureAsync(StoredProcedureModel procedure)
    {
        if (procedure == null)
        {
            return;
        }

        var connection = GetActiveConnection();
        if (connection == null)
        {
            return;
        }

        ApplySchemaContext(procedure.Schema);
        StoredProcedureDetailViewModel.CurrentConnection = connection;
        TableDetailViewModel.ShowObjectList = true;
        ViewDetailViewModel.ShowObjectList = true;
        StoredProcedureDetailViewModel.ShowObjectList = false;
        TableDetailViewModel.SelectedTable = null;
        ViewDetailViewModel.SelectedView = null;
        ShowOverviewPage = false;

        if (!string.IsNullOrWhiteSpace(procedure.Schema))
        {
            var hasProcedureInWorkspace = StoredProcedureDetailViewModel.Procedures.Any(existingProcedure =>
                existingProcedure.Schema == procedure.Schema &&
                existingProcedure.Name == procedure.Name);

            if (!hasProcedureInWorkspace)
            {
                await StoredProcedureDetailViewModel.LoadProceduresBySchemaAsync(procedure.Schema);
            }
        }

        CurrentWorkspaceMode = DetailWorkspaceMode.Procedure;
        StoredProcedureDetailViewModel.SelectedProcedure = StoredProcedureDetailViewModel.Procedures.FirstOrDefault(existingProcedure =>
            existingProcedure.Schema == procedure.Schema &&
            existingProcedure.Name == procedure.Name) ?? procedure;
    }

    [RelayCommand]
    private async Task OpenTableFromSchemaOverviewAsync(TableModel? table)
    {
        if (table != null)
        {
            await ActivateTableAsync(table);
        }
    }

    [RelayCommand]
    private async Task OpenViewFromSchemaOverviewAsync(Models.ViewModel? view)
    {
        if (view != null)
        {
            await ActivateViewAsync(view);
        }
    }

    [RelayCommand]
    private async Task OpenProcedureFromSchemaOverviewAsync(StoredProcedureModel? procedure)
    {
        if (procedure != null)
        {
            await ActivateProcedureAsync(procedure);
        }
    }

    private void ApplySchemaContext(string? schemaName)
    {
        CurrentSchemaName = schemaName;
        CurrentSchema = string.IsNullOrWhiteSpace(schemaName)
            ? null
            : Schemas.FirstOrDefault(schema => schema.Name == schemaName) ?? new SchemaModel { Name = schemaName };
    }

    private async Task LoadSchemasAsync(ConnectionConfig config)
    {
        IsLoading = true;
        LoadingText = $"正在加载 {config.Name} 的架构信息...";

        try
        {
            if (config.DatabaseType == DatabaseType.MySql)
            {
                var schemaName = string.IsNullOrWhiteSpace(config.Database)
                    ? "default"
                    : config.Database;

                var mySqlSchemas = new List<SchemaModel>
                {
                    new()
                    {
                        Name = schemaName,
                        Owner = "MySql",
                        TableCount = 0,
                        IsDefault = true
                    }
                };

                Schemas = new ObservableCollection<SchemaModel>(mySqlSchemas);
                LoadingText = $"已为 {config.Name} 加载 1 个架构。";
                LoggingService.LogInfo($"Loaded MySql runtime schema {schemaName} for {config.Name}.");
                return;
            }

            var (success, schemas, message) = await _databaseService.GetSchemasAsync(config);
            if (!success)
            {
                LoadingText = message;
                LoggingService.LogError($"Failed to load schemas for {config.Name}: {message}");
                return;
            }

            Schemas = new ObservableCollection<SchemaModel>(schemas);
            LoadingText = message;
            LoggingService.LogInfo($"Loaded {schemas.Count} schemas for {config.Name}.");
        }
        catch (Exception ex)
        {
            LoadingText = $"架构加载失败：{ex.Message}";
            LoggingService.LogError($"Schema load failed for {config.Name}.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ChangeSort(string sortMode)
    {
        SortMode = sortMode;
        Connections = new ObservableCollection<ConnectionConfig>(SortConnections(Connections));
        LoggingService.LogInfo($"Changed connection sort mode to {sortMode}.");
    }

    [RelayCommand]
    private async Task ExportDocumentAsync(string? mode)
    {
        var connection = GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        if (MainWindow == null)
        {
            LoggingService.LogWarning("MainWindow is not available.");
            return;
        }

        var normalizedMode = (mode ?? string.Empty).Trim().ToLowerInvariant();
        var exportWholeDatabase = normalizedMode.StartsWith("database-", StringComparison.Ordinal);
        var exportAsMarkdown = normalizedMode.EndsWith("markdown", StringComparison.Ordinal);

        if (!exportWholeDatabase && string.IsNullOrWhiteSpace(CurrentSchemaName))
        {
            ToastService.ShowWarning("导出当前架构前，请先在对象树中选择一个架构。", 3000);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(MainWindow);
        if (topLevel?.StorageProvider == null)
        {
            LoggingService.LogWarning("Storage provider is unavailable for document export.");
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = exportAsMarkdown ? "导出 Markdown 文档" : "导出 Excel 文档",
            FileTypeChoices = new[]
            {
                exportAsMarkdown
                    ? new FilePickerFileType("Markdown 文档") { Patterns = new[] { "*.md" } }
                    : new FilePickerFileType("Excel 文档") { Patterns = new[] { "*.xlsx" } }
            },
            DefaultExtension = exportAsMarkdown ? "md" : "xlsx",
            SuggestedFileName = BuildSuggestedExportFileName(exportWholeDatabase, exportAsMarkdown)
        });

        if (file == null)
        {
            return;
        }

        IsLoading = true;
        LoadingText = exportWholeDatabase
            ? "正在准备导出整个数据库..."
            : $"正在准备导出架构 {CurrentSchemaName} ...";

        try
        {
            var (success, tables, views, procedures, tableColumnsMap, tableIndexesMap, message) = await BuildExportPayloadAsync(connection, exportWholeDatabase);
            if (!success)
            {
                LoadingText = message;
                ToastService.ShowError(message, 5000);
                return;
            }

            if (tables.Count == 0 && views.Count == 0 && procedures.Count == 0)
            {
                LoadingText = "没有可导出的数据。";
                ToastService.ShowWarning("没有可导出的数据。", 3000);
                return;
            }

            var documentName = exportWholeDatabase
                ? connection.Name
                : $"{connection.Name} - {CurrentSchemaName}";

            bool exported;
            if (exportAsMarkdown)
            {
                exported = await _documentExportService.ExportToMarkdownAsync(
                    file.Path.LocalPath,
                    documentName,
                    tables,
                    tableColumnsMap,
                    views,
                    procedures,
                    tableIndexesMap);
            }
            else
            {
                exported = await _documentExportService.ExportToExcelAsync(
                    file.Path.LocalPath,
                    documentName,
                    tables,
                    tableColumnsMap,
                    views,
                    procedures,
                    tableIndexesMap);
            }

            if (!exported)
            {
                LoadingText = "文档导出失败，请查看日志获取详情。";
                ToastService.ShowError("文档导出失败，请查看日志获取详情。", 5000);
                return;
            }

            var totalObjects = tables.Count + views.Count + procedures.Count;
            LoadingText = $"已导出 {totalObjects} 个对象到 {file.Name}。";
            ToastService.ShowSuccess($"已导出 {totalObjects} 个对象到 {file.Name}。", 4000);
        }
        catch (Exception ex)
        {
            LoadingText = "文档导出失败。";
            LoggingService.LogError("文档导出失败。", ex);
            ToastService.ShowError($"文档导出失败：{ex.Message}", 6000);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenExportDialogAsync()
    {
        var connection = GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        if (MainWindow == null)
        {
            LoggingService.LogWarning("MainWindow is not available.");
            return;
        }

        var dialogViewModel = new ExportDialogViewModel(connection, SelectedDatabaseName, CurrentSchemaName, _lastDocumentExportDirectory);
        var dialog = new ExportDialog(dialogViewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var exportRequest = await dialog.ShowDialog<ExportDialogResultDto?>(MainWindow);
        if (exportRequest == null)
        {
            return;
        }

        _lastDocumentExportDirectory = exportRequest.OutputDirectory;
        IsLoading = true;
        LoadingText = "正在准备导出文档...";

        try
        {
            var (success, tables, views, procedures, tableColumnsMap, tableIndexesMap, message) =
                await BuildExportPayloadAsync(connection, exportRequest.SelectedObjects);
            if (!success)
            {
                LoadingText = message;
                ToastService.ShowError(message, 5000);
                return;
            }

            if (tables.Count == 0 && views.Count == 0 && procedures.Count == 0)
            {
                LoadingText = "没有可导出的数据。";
                ToastService.ShowWarning("没有可导出的数据。", 3000);
                return;
            }

            var exportFilePath = BuildExportFilePath(exportRequest);
            var exported = exportRequest.DocumentType switch
            {
                ExportDocumentType.Excel => await _documentExportService.ExportToExcelAsync(
                    exportFilePath,
                    exportRequest.DocumentName,
                    tables,
                    tableColumnsMap,
                    views,
                    procedures,
                    tableIndexesMap),
                ExportDocumentType.Markdown => await _documentExportService.ExportToMarkdownAsync(
                    exportFilePath,
                    exportRequest.DocumentName,
                    tables,
                    tableColumnsMap,
                    views,
                    procedures,
                    tableIndexesMap),
                _ => false
            };

            if (!exported)
            {
                LoadingText = "文档导出失败，请查看日志获取详情。";
                ToastService.ShowError("文档导出失败，请查看日志获取详情。", 5000);
                return;
            }

            var totalObjects = tables.Count + views.Count + procedures.Count;
            var exportedFileName = Path.GetFileName(exportFilePath);
            LoadingText = $"已导出 {totalObjects} 个对象到 {exportedFileName}。";
            ToastService.ShowSuccess($"已导出 {totalObjects} 个对象到 {exportedFileName}。", 4000);
        }
        catch (Exception ex)
        {
            LoadingText = "文档导出失败。";
            LoggingService.LogError("Document export failed.", ex);
            ToastService.ShowError($"文档导出失败：{ex.Message}", 6000);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportConnectionsAsync()
    {
        try
        {
            if (MainWindow == null)
            {
                LoggingService.LogWarning("MainWindow is not available.");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.StorageProvider == null)
            {
                LoggingService.LogWarning("Storage provider is unavailable.");
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出连接配置",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*", "*.*" } }
                },
                DefaultExtension = "json",
                SuggestedFileName = $"connections_{DateTime.Now:yyyyMMdd_HHmmss}"
            });

            if (file == null)
            {
                return;
            }

            EncryptPasswordsInPlace(Connections);
            try
            {
                var json = JsonSerializer.Serialize(Connections.ToList(), CreateJsonOptions());
                await File.WriteAllTextAsync(file.Path.LocalPath, json);
            }
            finally
            {
                DecryptPasswordsInPlace(Connections);
            }

            LoggingService.LogOperation($"Exported connection config to {file.Path.LocalPath}.");
            ToastService.ShowSuccess($"已导出 {Connections.Count} 个连接。", 3000);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("导出连接失败。", ex);
            ToastService.ShowError($"导出失败：{ex.Message}", 6000);
        }
    }

    [RelayCommand]
    private async Task GenerateCodeAsync(string? mode)
    {
        var connection = GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentSchemaName))
        {
            ToastService.ShowWarning("生成代码前请先在对象树中选择架构。", 3000);
            return;
        }

        if (MainWindow == null)
        {
            LoggingService.LogWarning("MainWindow is not available.");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(MainWindow);
        if (topLevel?.StorageProvider == null)
        {
            LoggingService.LogWarning("Storage provider is unavailable for code generation.");
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择代码输出目录",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        var outputRootPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputRootPath))
        {
            ToastService.ShowWarning("所选输出目录不是本地文件系统路径。", 4000);
            return;
        }

        var normalizedMode = (mode ?? "all").Trim().ToLowerInvariant();
        var generateEntities = normalizedMode is "entity" or "entities" or "all";
        var generateRepositories = normalizedMode is "repository" or "repositories" or "all";
        var generateControllers = normalizedMode is "controller" or "controllers" or "all";

        if (!generateEntities && !generateRepositories && !generateControllers)
        {
            ToastService.ShowWarning($"不支持的代码生成模式：{mode}", 4000);
            return;
        }

        IsLoading = true;
        LoadingText = $"正在准备为架构 {CurrentSchemaName} 生成代码...";

        try
        {
            var (success, tables, tableColumnsMap, message) = await BuildCodeGenerationPayloadAsync(connection, CurrentSchemaName);
            if (!success)
            {
                LoadingText = message;
                ToastService.ShowError(message, 5000);
                return;
            }

            if (tables.Count == 0)
            {
                LoadingText = "没有可生成代码的数据表。";
                ToastService.ShowWarning("没有可生成代码的数据表。", 3000);
                return;
            }

            var namespaceRoot = BuildCodeGenerationNamespace(connection.Name, CurrentSchemaName);
            var generatedFileCount = 0;
            var failedItems = new List<string>();

            foreach (var table in tables.OrderBy(table => table.Name))
            {
                tableColumnsMap.TryGetValue(table.Name, out var columns);
                columns ??= new List<ColumnModel>();

                if (generateEntities)
                {
                    LoadingText = $"正在生成实体类：{table.Name} ...";
                    var entityPath = Path.Combine(outputRootPath, "Entities", $"{SanitizeCodeIdentifier(table.Name)}.cs");
                    var entitySuccess = await _codeGenerationService.GenerateEntityClassAsync(entityPath, table.Name, columns, $"{namespaceRoot}.Entities");
                    if (entitySuccess)
                    {
                        generatedFileCount++;
                    }
                    else
                    {
                        failedItems.Add($"Entity:{table.Name}");
                    }
                }

                if (generateRepositories)
                {
                    LoadingText = $"正在生成仓储类：{table.Name} ...";
                    var repositoryPath = Path.Combine(outputRootPath, "Repositories", $"{SanitizeCodeIdentifier(table.Name)}Repository.cs");
                    var repositorySuccess = await _codeGenerationService.GenerateRepositoryAsync(repositoryPath, table.Name, $"{namespaceRoot}.Repositories");
                    if (repositorySuccess)
                    {
                        generatedFileCount++;
                    }
                    else
                    {
                        failedItems.Add($"Repository:{table.Name}");
                    }
                }

                if (generateControllers)
                {
                    LoadingText = $"正在生成控制器：{table.Name} ...";
                    var controllerPath = Path.Combine(outputRootPath, "Controllers", $"{SanitizeCodeIdentifier(table.Name)}Controller.cs");
                    var controllerSuccess = await _codeGenerationService.GenerateControllerAsync(controllerPath, table.Name, $"{namespaceRoot}.Controllers");
                    if (controllerSuccess)
                    {
                        generatedFileCount++;
                    }
                    else
                    {
                        failedItems.Add($"Controller:{table.Name}");
                    }
                }
            }

            if (failedItems.Count > 0)
            {
                LoadingText = $"已生成 {generatedFileCount} 个文件，失败 {failedItems.Count} 项。";
                ToastService.ShowWarning($"已生成 {generatedFileCount} 个文件，但有 {failedItems.Count} 项失败，请查看日志。", 6000);
                LoggingService.LogWarning($"Code generation partial failure: {string.Join(", ", failedItems)}");
                return;
            }

            LoadingText = $"已生成 {generatedFileCount} 个文件到 {outputRootPath}。";
            ToastService.ShowSuccess($"已为架构 {CurrentSchemaName} 生成 {generatedFileCount} 个文件。", 4000);
            LoggingService.LogOperation($"Generated {generatedFileCount} code files to {outputRootPath}.");
        }
        catch (Exception ex)
        {
            LoadingText = "代码生成失败。";
            LoggingService.LogError("代码生成失败。", ex);
            ToastService.ShowError($"代码生成失败：{ex.Message}", 6000);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportConnectionsAsync()
    {
        try
        {
            if (MainWindow == null)
            {
                LoggingService.LogWarning("MainWindow is not available.");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel?.StorageProvider == null)
            {
                LoggingService.LogWarning("Storage provider is unavailable.");
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "导入连接配置",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*", "*.*" } }
                },
                AllowMultiple = false
            });

            if (files.Count == 0)
            {
                return;
            }

            var json = await File.ReadAllTextAsync(files[0].Path.LocalPath);
            var importedConnections = JsonSerializer.Deserialize<List<ConnectionConfig>>(json, CreateJsonOptions());
            if (importedConnections == null || importedConnections.Count == 0)
            {
                ToastService.ShowWarning("所选文件中没有有效的连接配置。", 4000);
                return;
            }

            foreach (var connection in importedConnections)
            {
                DecryptConnectionPassword(connection);
            }

            var existingNames = Connections.Select(connection => connection.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var importedCount = 0;
            var skippedCount = 0;

            foreach (var connection in importedConnections)
            {
                if (existingNames.Contains(connection.Name))
                {
                    skippedCount++;
                    continue;
                }

                Connections.Add(connection);
                existingNames.Add(connection.Name);
                importedCount++;
            }

            SaveConnections();

            var message = skippedCount > 0
                ? $"已导入 {importedCount} 个连接，跳过 {skippedCount} 个重复项。"
                : $"已导入 {importedCount} 个连接。";

            LoggingService.LogOperation(message);
            ToastService.ShowSuccess(message, 4000);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("导入连接失败。", ex);
            ToastService.ShowError($"导入失败：{ex.Message}", 6000);
        }
    }

    [RelayCommand]
    private void AddGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return;
        }

        Groups.Add(new ConnectionGroup
        {
            Name = groupName.Trim(),
            Color = "#E3EFE8"
        });

        SaveGroups();
        LoggingService.LogInfo($"Added group: {groupName}");
    }

    [RelayCommand]
    private void DeleteGroup(string groupId)
    {
        var group = Groups.FirstOrDefault(item => item.Id == groupId);
        if (group == null || group.IsDefault)
        {
            return;
        }

        var groupName = group.Name;
        Groups.Remove(group);

        foreach (var connection in Connections.Where(connection => connection.GroupId == groupId))
        {
            connection.GroupId = null;
            connection.GroupName = null;
        }

        SaveGroups();
        SaveConnections();

        LoggingService.LogInfo($"Deleted group: {groupName}");
    }

    [RelayCommand]
    private void ChangeGroupFilter(string? groupId)
    {
        SelectedGroupId = groupId;
        LoggingService.LogInfo($"Changed group filter to {groupId ?? "All"}.");
    }

    [RelayCommand]
    private async Task SelectSchemaAsync(SchemaModel schema)
    {
        if (schema == null)
        {
            ToastService.ShowWarning("请先选择架构。", 2000);
            return;
        }

        var connection = GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        try
        {
            CurrentSchemaName = schema.Name;

            TableDetailViewModel.CurrentConnection = connection;
            ViewDetailViewModel.CurrentConnection = connection;
            StoredProcedureDetailViewModel.CurrentConnection = connection;

            await TableDetailViewModel.LoadTablesBySchemaAsync(schema.Name);
            await ViewDetailViewModel.LoadViewsBySchemaAsync(schema.Name);
            await StoredProcedureDetailViewModel.LoadProceduresBySchemaAsync(schema.Name);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to load the selected schema.", ex);
            ToastService.ShowError($"架构加载失败：{ex.Message}", 3000);
        }
    }

    private async Task<(bool Success, List<TableModel> Tables, List<ViewModel> Views, List<StoredProcedureModel> Procedures, Dictionary<string, List<ColumnModel>> TableColumnsMap, Dictionary<string, List<IndexModel>> TableIndexesMap, string Message)> BuildExportPayloadAsync(
        ConnectionConfig connection,
        bool exportWholeDatabase)
    {
        var tables = new List<TableModel>();
        var views = new List<ViewModel>();
        var procedures = new List<StoredProcedureModel>();
        var tableColumnsMap = new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase);
        var tableIndexesMap = new Dictionary<string, List<IndexModel>>(StringComparer.OrdinalIgnoreCase);

        List<string> schemaNames;
        if (connection.DatabaseType == DatabaseType.MySql)
        {
            var mySqlSchemaName = string.IsNullOrWhiteSpace(connection.Database)
                ? CurrentSchemaName
                : connection.Database;

            if (string.IsNullOrWhiteSpace(mySqlSchemaName))
            {
                return (false, tables, views, procedures, tableColumnsMap, tableIndexesMap, "MySql 当前数据库为空，无法导出。");
            }

            schemaNames = new List<string> { mySqlSchemaName };
        }
        else if (exportWholeDatabase)
        {
            var (success, schemas, message) = await _databaseService.GetSchemasAsync(connection);
            if (!success)
            {
                return (false, tables, views, procedures, tableColumnsMap, tableIndexesMap, message);
            }

            schemaNames = schemas.Select(schema => schema.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            schemaNames = new List<string> { CurrentSchemaName! };
        }

        foreach (var schemaName in schemaNames)
        {
            LoadingText = $"正在加载架构 {schemaName} 的数据表...";
            var (tableSuccess, schemaTables, tableMessage) = await _databaseService.GetTablesAsync(connection, schemaName);
            if (!tableSuccess)
            {
                return (false, tables, views, procedures, tableColumnsMap, tableIndexesMap, tableMessage);
            }

            foreach (var table in schemaTables.OrderBy(table => table.Name))
            {
                tables.Add(table);

                LoadingText = $"正在加载 {table.Schema}.{table.Name} 的字段...";
                var (columnSuccess, columns, columnMessage) = await _databaseService.GetColumnsAsync(connection, table.Schema, table.Name);
                if (!columnSuccess)
                {
                    LoggingService.LogWarning($"Column export fallback for {table.Schema}.{table.Name}: {columnMessage}");
                    tableColumnsMap[BuildTableExportKey(table)] = new List<ColumnModel>();
                    continue;
                }

                tableColumnsMap[BuildTableExportKey(table)] = columns.OrderBy(column => column.OrdinalPosition).ToList();

                LoadingText = $"正在加载 {table.Schema}.{table.Name} 的索引...";
                var (indexSuccess, indexes, indexMessage) = await _databaseService.GetIndexesAsync(connection, table.Schema, table.Name);
                if (!indexSuccess)
                {
                    LoggingService.LogWarning($"Index export fallback for {table.Schema}.{table.Name}: {indexMessage}");
                    tableIndexesMap[BuildTableExportKey(table)] = new List<IndexModel>();
                    continue;
                }

                tableIndexesMap[BuildTableExportKey(table)] = indexes.ToList();
            }

        }

        var totalObjects = tables.Count;
        return (true, tables, views, procedures, tableColumnsMap, tableIndexesMap, $"已为导出准备 {totalObjects} 个对象。");
    }

    private async Task<(bool Success, List<TableModel> Tables, List<ViewModel> Views, List<StoredProcedureModel> Procedures, Dictionary<string, List<ColumnModel>> TableColumnsMap, Dictionary<string, List<IndexModel>> TableIndexesMap, string Message)> BuildExportPayloadAsync(
        ConnectionConfig connection,
        IReadOnlyCollection<ExportSelectedObjectDto> selectedObjects)
    {
        var tables = new List<TableModel>();
        var views = new List<ViewModel>();
        var procedures = new List<StoredProcedureModel>();
        var tableColumnsMap = new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase);
        var tableIndexesMap = new Dictionary<string, List<IndexModel>>(StringComparer.OrdinalIgnoreCase);

        if (selectedObjects.Count == 0)
        {
            return (false, tables, views, procedures, tableColumnsMap, tableIndexesMap, "请至少选择一个导出对象。");
        }

        var selectedTablesBySchema = selectedObjects
            .Where(item => item.ObjectType == ExportObjectType.Table)
            .GroupBy(item => item.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var schemaNames = selectedObjects
            .Select(item => item.SchemaName)
            .Where(schemaName => !string.IsNullOrWhiteSpace(schemaName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var schemaName in schemaNames)
        {
            if (selectedTablesBySchema.TryGetValue(schemaName, out var selectedTableNames) && selectedTableNames.Count > 0)
            {
                LoadingText = $"正在加载表 {schemaName}...";
                var (tableSuccess, schemaTables, tableMessage) = await _databaseService.GetTablesAsync(connection, schemaName);
                if (!tableSuccess)
                {
                    return (false, tables, views, procedures, tableColumnsMap, tableIndexesMap, tableMessage);
                }

                foreach (var table in schemaTables
                             .Where(table => selectedTableNames.Contains(table.Name))
                             .OrderBy(table => table.Name))
                {
                    tables.Add(table);

                    LoadingText = $"正在加载字段 {table.Schema}.{table.Name}...";
                    var (columnSuccess, columns, columnMessage) = await _databaseService.GetColumnsAsync(connection, table.Schema, table.Name);
                    if (!columnSuccess)
                    {
                        LoggingService.LogWarning($"Column export fallback for {table.Schema}.{table.Name}: {columnMessage}");
                        tableColumnsMap[BuildTableExportKey(table)] = new List<ColumnModel>();
                    }
                    else
                    {
                        tableColumnsMap[BuildTableExportKey(table)] = columns.OrderBy(column => column.OrdinalPosition).ToList();
                    }

                    LoadingText = $"正在加载索引 {table.Schema}.{table.Name}...";
                    var (indexSuccess, indexes, indexMessage) = await _databaseService.GetIndexesAsync(connection, table.Schema, table.Name);
                    if (!indexSuccess)
                    {
                        LoggingService.LogWarning($"Index export fallback for {table.Schema}.{table.Name}: {indexMessage}");
                        tableIndexesMap[BuildTableExportKey(table)] = new List<IndexModel>();
                    }
                    else
                    {
                        tableIndexesMap[BuildTableExportKey(table)] = indexes.ToList();
                    }
                }
            }

        }

        var totalObjects = tables.Count;
        return totalObjects == 0
            ? (false, tables, views, procedures, tableColumnsMap, tableIndexesMap, "未匹配到可导出的对象。")
            : (true, tables, views, procedures, tableColumnsMap, tableIndexesMap, $"已为导出准备 {totalObjects} 个对象。");
    }

    private async Task<(bool Success, List<TableModel> Tables, Dictionary<string, List<ColumnModel>> TableColumnsMap, string Message)> BuildCodeGenerationPayloadAsync(
        ConnectionConfig connection,
        string schemaName)
    {
        var (tableSuccess, tables, tableMessage) = await _databaseService.GetTablesAsync(connection, schemaName);
        if (!tableSuccess)
        {
            return (false, new List<TableModel>(), new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase), tableMessage);
        }

        var tableColumnsMap = new Dictionary<string, List<ColumnModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables.OrderBy(table => table.Name))
        {
            LoadingText = $"正在加载 {table.Schema}.{table.Name} 的字段...";
            var (columnSuccess, columns, columnMessage) = await _databaseService.GetColumnsAsync(connection, table.Schema, table.Name);
            if (!columnSuccess)
            {
                LoggingService.LogWarning($"Code generation column fallback for {table.Schema}.{table.Name}: {columnMessage}");
                tableColumnsMap[table.Name] = new List<ColumnModel>();
                continue;
            }

            tableColumnsMap[table.Name] = columns.OrderBy(column => column.OrdinalPosition).ToList();
        }

        return (true, tables, tableColumnsMap, $"Prepared {tables.Count} tables for code generation.");
    }

    private string BuildSuggestedExportFileName(bool exportWholeDatabase, bool exportAsMarkdown)
    {
        var connection = GetActiveConnection();
        var scope = exportWholeDatabase
            ? connection?.Name ?? SelectedConnection?.Name ?? "database"
            : $"{connection?.Name ?? SelectedConnection?.Name}_{CurrentSchemaName}";

        var safeScope = SanitizeFileName(scope);
        var format = exportAsMarkdown ? "markdown" : "excel";
        return $"smartsql_{safeScope}_{format}_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    private ConnectionConfig? GetActiveConnection()
    {
        return ActiveConnectionContext ?? SelectedConnection;
    }

    private async Task LoadAvailableDatabasesAsync(ConnectionConfig connection, string? preferredDatabase)
    {
        AvailableDatabases.Clear();

        try
        {
            var (success, databases, _) = await _databaseService.GetDatabaseNamesAsync(connection);
            if (SelectedConnection != connection)
            {
                return;
            }

            var mergedDatabases = databases
                .Where(database => !string.IsNullOrWhiteSpace(database))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredDatabase) &&
                !mergedDatabases.Contains(preferredDatabase, StringComparer.OrdinalIgnoreCase))
            {
                mergedDatabases.Insert(0, preferredDatabase);
            }

            foreach (var database in mergedDatabases)
            {
                AvailableDatabases.Add(database);
            }

            if (!success && !string.IsNullOrWhiteSpace(preferredDatabase) && AvailableDatabases.Count == 0)
            {
                AvailableDatabases.Add(preferredDatabase);
            }

            if (string.IsNullOrWhiteSpace(preferredDatabase))
            {
                preferredDatabase = AvailableDatabases.FirstOrDefault();
            }

            _suppressDatabaseSelectionChanged = true;
            SelectedDatabaseName = preferredDatabase;
            _suppressDatabaseSelectionChanged = false;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to load databases for {connection.Name}.", ex);

            if (!string.IsNullOrWhiteSpace(preferredDatabase))
            {
                AvailableDatabases.Add(preferredDatabase);
                _suppressDatabaseSelectionChanged = true;
                SelectedDatabaseName = preferredDatabase;
                _suppressDatabaseSelectionChanged = false;
            }
        }
    }

    private static ConnectionConfig CreateRuntimeConnection(ConnectionConfig source, string? databaseName)
    {
        return new ConnectionConfig
        {
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            Database = string.IsNullOrWhiteSpace(databaseName) ? source.Database : databaseName,
            UseWindowsAuthentication = source.UseWindowsAuthentication,
            GroupId = source.GroupId,
            GroupName = source.GroupName,
            Color = source.Color,
            LastUsedTime = source.LastUsedTime,
            UseCount = source.UseCount
        };
    }

    private void ResetWorkspaceState()
    {
        Schemas.Clear();
        CurrentSchemaName = null;
        CurrentSchema = null;
        ShowOverviewPage = true;
        CurrentWorkspaceMode = DetailWorkspaceMode.Table;
        TableDetailViewModel.ShowObjectList = true;
        ViewDetailViewModel.ShowObjectList = true;
        StoredProcedureDetailViewModel.ShowObjectList = true;
        TableDetailViewModel.SelectedTable = null;
        ViewDetailViewModel.SelectedView = null;
        StoredProcedureDetailViewModel.SelectedProcedure = null;
    }

    private static string BuildTableExportKey(TableModel table)
    {
        return $"{table.Schema}.{table.Name}";
    }

    private static string BuildExportFilePath(ExportDialogResultDto exportRequest)
    {
        var safeDocumentName = SanitizeFileName(exportRequest.DocumentName);
        var extension = exportRequest.DocumentType switch
        {
            ExportDocumentType.Markdown => ".md",
            _ => ".xlsx"
        };

        var targetFilePath = Path.Combine(exportRequest.OutputDirectory, $"{safeDocumentName}{extension}");

        if (!File.Exists(targetFilePath))
        {
            return targetFilePath;
        }

        return Path.Combine(exportRequest.OutputDirectory, $"{safeDocumentName}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    }

    private static string BuildCodeGenerationNamespace(string connectionName, string schemaName)
    {
        var safeConnection = SanitizeCodeIdentifier(connectionName);
        var safeSchema = SanitizeCodeIdentifier(schemaName);
        return $"SmartSQL.Generated.{safeConnection}.{safeSchema}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "export" : sanitized;
    }

    private static string SanitizeCodeIdentifier(string value)
    {
        var sanitized = SanitizeFileName(value)
            .Replace(' ', '_')
            .Replace('-', '_');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "GeneratedItem";
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = $"Generated_{sanitized}";
        }

        return sanitized;
    }

    private async Task ShowErrorMessageAsync(string title, string message)
    {
        if (MainWindow == null)
        {
            return;
        }

        try
        {
            var messageBox = new MessageBox
            {
                Title = title,
                Message = message,
                Buttons = MessageBoxButtons.OK,
                DefaultButton = MessageBoxButtonType.OK
            };

            await messageBox.ShowDialogAsync(MainWindow);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to show message box.", ex);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    private static void EncryptPasswordsInPlace(IEnumerable<ConnectionConfig> connections)
    {
        foreach (var connection in connections)
        {
            if (!string.IsNullOrWhiteSpace(connection.Password))
            {
                connection.Password = connection.GetEncryptedPassword();
            }
        }
    }

    private static void DecryptPasswordsInPlace(IEnumerable<ConnectionConfig> connections)
    {
        foreach (var connection in connections)
        {
            DecryptConnectionPassword(connection);
        }
    }

    private static void DecryptConnectionPassword(ConnectionConfig connection)
    {
        if (!string.IsNullOrWhiteSpace(connection.Password))
        {
            connection.SetDecryptedPassword(connection.Password);
        }
    }
}
