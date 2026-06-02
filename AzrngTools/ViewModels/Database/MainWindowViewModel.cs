using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;
using AzrngTools.Views.Database.Workbench;
using DatabaseViewModel = AzrngTools.Models.Database.ViewModel;

namespace AzrngTools.ViewModels.Database;

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
    private readonly IDatabaseService _databaseService;
    private readonly IDocumentExportService _documentExportService;
    private readonly ICodeGenerationService _codeGenerationService;
    private readonly IConnectionConfigurationService _connectionConfigurationService;
    private readonly IDatabaseExportPayloadService _databaseExportPayloadService;
    private readonly ICodeGenerationPayloadService _codeGenerationPayloadService;
    private readonly IConnectionGroupConfigurationService _connectionGroupConfigurationService;
    private readonly IDatabaseWorkbenchNamingService _databaseWorkbenchNamingService;
    private readonly IDatabaseConnectionContextService _databaseConnectionContextService;
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
    private NotifyCollectionChangedEventHandler? _availableDatabasesChangedHandler;

    [ObservableProperty]
    private ObservableCollection<string> _filteredAvailableDatabases = new();

    [ObservableProperty]
    private string _databaseSearchText = string.Empty;

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
    private DatabaseBrowserViewModel _browserViewModel;

    [ObservableProperty]
    private TableDetailViewModel _tableDetailViewModel;

    [ObservableProperty]
    private ViewDetailViewModel _viewDetailViewModel;

    [ObservableProperty]
    private StoredProcedureDetailViewModel _storedProcedureDetailViewModel;

    [ObservableProperty]
    private SqlQueryViewModel _sqlQueryViewModel;

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
        : this(
            new DatabaseService(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null)
    {
    }

    public MainWindowViewModel(
        IDatabaseService databaseService,
        IDocumentExportService? documentExportService = null,
        ICodeGenerationService? codeGenerationService = null,
        IConnectionConfigurationService? connectionConfigurationService = null,
        IDatabaseExportPayloadService? databaseExportPayloadService = null,
        ICodeGenerationPayloadService? codeGenerationPayloadService = null,
        IConnectionGroupConfigurationService? connectionGroupConfigurationService = null,
        IDatabaseWorkbenchNamingService? databaseWorkbenchNamingService = null,
        IDatabaseConnectionContextService? databaseConnectionContextService = null,
        DatabaseBrowserViewModel? browserViewModel = null,
        TableDetailViewModel? tableDetailViewModel = null,
        ViewDetailViewModel? viewDetailViewModel = null,
        StoredProcedureDetailViewModel? storedProcedureDetailViewModel = null,
        SqlQueryViewModel? sqlQueryViewModel = null)
    {
        _databaseService = databaseService;
        _documentExportService = documentExportService ?? new DocumentExportService();
        _codeGenerationService = codeGenerationService ?? new CodeGenerationService();
        _connectionConfigurationService = connectionConfigurationService ?? new ConnectionConfigurationService();
        _databaseExportPayloadService = databaseExportPayloadService ?? new DatabaseExportPayloadService(databaseService);
        _codeGenerationPayloadService = codeGenerationPayloadService ?? new CodeGenerationPayloadService(databaseService);
        _connectionGroupConfigurationService = connectionGroupConfigurationService ?? new ConnectionGroupConfigurationService();
        _databaseWorkbenchNamingService = databaseWorkbenchNamingService ?? new DatabaseWorkbenchNamingService();
        _databaseConnectionContextService = databaseConnectionContextService ?? new DatabaseConnectionContextService();
        BrowserViewModel = browserViewModel ?? new DatabaseBrowserViewModel(databaseService);
        TableDetailViewModel = tableDetailViewModel ?? new TableDetailViewModel(databaseService);
        ViewDetailViewModel = viewDetailViewModel ?? new ViewDetailViewModel(databaseService);
        StoredProcedureDetailViewModel = storedProcedureDetailViewModel ?? new StoredProcedureDetailViewModel(databaseService);
        SqlQueryViewModel = sqlQueryViewModel ?? new SqlQueryViewModel(databaseService);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartDbSql");
        Directory.CreateDirectory(appDataDir);
        _configFilePath = Path.Combine(appDataDir, ConfigFileName);
        _groupsFilePath = Path.Combine(appDataDir, GroupsFileName);
        _availableDatabasesChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(HasAvailableDatabases));
            RefreshFilteredAvailableDatabases();
        };
        AvailableDatabases.CollectionChanged += _availableDatabasesChangedHandler;

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
            var groups = _connectionGroupConfigurationService.LoadGroups(_groupsFilePath);
            if (groups.Count == 0)
            {
                CreateDefaultGroup();
                return;
            }

            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);
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
        Groups.Clear();
        Groups.Add(_connectionGroupConfigurationService.CreateDefaultGroup());

        SaveGroups();
    }

    private void SaveGroups()
    {
        try
        {
            _connectionGroupConfigurationService.SaveGroups(_groupsFilePath, Groups);
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
                Connections.Clear();
                LoggingService.LogInfo("Connection config file does not exist yet.");
                return;
            }

            var connections = _connectionConfigurationService.LoadConnections(_configFilePath);

            var sortedConnections = _databaseConnectionContextService.SortConnections(connections, SortMode);
            Connections.Clear();
            foreach (var c in sortedConnections) Connections.Add(c);
            LoggingService.LogInfo($"Loaded {Connections.Count} connections.");
        }
        catch (Exception ex)
        {
            Connections.Clear();
            LoggingService.LogError("Failed to load connection configuration.", ex);
        }
    }

    private void SaveConnections()
    {
        try
        {
            _connectionConfigurationService.SaveConnections(_configFilePath, Connections);
            LoggingService.LogOperation($"Saved {Connections.Count} connections.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to save connection configuration.", ex);
            throw;
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

            var vm = new ConnectionDialogViewModel(Connections, SaveConnections, SelectedConnection, _databaseService);
            var result = await Ursa.Controls.Dialog.ShowCustomAsync<ConnectionDialog, ConnectionDialogViewModel, ConnectionConfig?>(
                vm, MainWindow, new Ursa.Controls.DialogOptions { CanResize = false });
            if (result == null)
            {
                return;
            }

            result.UpdateUsageStats();
            var targetConnection = _databaseConnectionContextService.ResolveConnectionFromCollection(Connections, result);
            await ApplySelectedConnectionAsync(targetConnection);

            ToastService.ShowSuccess($"已切换到连接：{targetConnection.Name}", 2000);
            LoggingService.LogOperation($"Selected connection: {targetConnection.Name}");
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
            var result = await Ursa.Controls.MessageBox.ShowAsync(
                MainWindow,
                string.Concat("确定删除连接“", SelectedConnection.Name, "”？", Environment.NewLine, Environment.NewLine, "此操作无法撤销。"),
                "删除连接",
                icon: Ursa.Controls.MessageBoxIcon.Question,
                button: Ursa.Controls.MessageBoxButton.YesNo);
            if (result != Ursa.Controls.MessageBoxResult.Yes)
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
        ResetConnectionContextState(value);
    }

    partial void OnSelectedConnectionChanged(ConnectionConfig? value)
    {
        OnPropertyChanged(nameof(CurrentConnectionLabel));
        OnPropertyChanged(nameof(CompactConnectionLabel));

        if (value == null)
        {
            return;
        }

        RunConnectionContextTask(
            InitializeConnectionContextAsync(value),
            $"初始化连接上下文失败：{value.Name}",
            "连接初始化失败");
    }

    private void ResetConnectionContextState(ConnectionConfig? nextConnection)
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
        BrowserViewModel.Reset();
        ResetWorkspaceState();

        if (nextConnection == null)
        {
            ShowOverviewPage = true;
        }

        OnPropertyChanged(nameof(CurrentConnectionLabel));
        OnPropertyChanged(nameof(CompactConnectionLabel));
    }

    private async Task ApplySelectedConnectionAsync(ConnectionConfig connection)
    {
        if (!ReferenceEquals(SelectedConnection, connection))
        {
            SelectedConnection = null;
            SelectedConnection = connection;
            return;
        }

        ResetConnectionContextState(connection);
        await InitializeConnectionContextAsync(connection);
    }

    partial void OnSelectedDatabaseNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CurrentConnectionLabel));
        OnPropertyChanged(nameof(CompactConnectionLabel));
        DatabaseSearchText = value ?? string.Empty;

        if (_suppressDatabaseSelectionChanged || SelectedConnection == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        RunConnectionContextTask(
            SwitchDatabaseAsync(value),
            $"切换数据库失败：{value}",
            "数据库切换失败");
    }

    partial void OnDatabaseSearchTextChanged(string value)
    {
        RefreshFilteredAvailableDatabases();
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

        var runtimeConnection = _databaseConnectionContextService.CreateRuntimeConnection(connection, SelectedDatabaseName);
        ActiveConnectionContext = runtimeConnection;
        await LoadConnectionContextAsync(runtimeConnection);
    }

    private async void RunConnectionContextTask(Task task, string logMessage, string userMessage)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            LoadingText = $"{userMessage}：{ex.Message}";
            LoggingService.LogError(logMessage, ex);
            ToastService.ShowError($"{userMessage}：{ex.Message}", 5000);
        }
    }

    private async Task SwitchDatabaseAsync(string databaseName)
    {
        if (SelectedConnection == null)
        {
            return;
        }

        var runtimeConnection = _databaseConnectionContextService.CreateRuntimeConnection(SelectedConnection, databaseName);
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

        if (BrowserViewModel.FirstRootNode == null &&
            !string.IsNullOrWhiteSpace(BrowserViewModel.LoadingText) &&
            (BrowserViewModel.LoadingText.StartsWith("加载失败", StringComparison.Ordinal) ||
             BrowserViewModel.LoadingText.StartsWith("加载异常", StringComparison.Ordinal)))
        {
            ToastService.ShowWarning($"对象浏览器未能加载：{BrowserViewModel.LoadingText}", 4000);
        }
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

    public async Task ActivateViewAsync(DatabaseViewModel view)
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
    private async Task OpenViewFromSchemaOverviewAsync(DatabaseViewModel? view)
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
            var (success, schemas, message) = await _databaseService.GetSchemasAsync(config);
            if (!success)
            {
                LoadingText = message;
                LoggingService.LogError($"Failed to load schemas for {config.Name}: {message}");
                return;
            }

            Schemas.Clear();
            foreach (var s in schemas) Schemas.Add(s);
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
        var sorted = _databaseConnectionContextService.SortConnections(Connections, SortMode);
        Connections.Clear();
        foreach (var c in sorted) Connections.Add(c);
        LoggingService.LogInfo($"Changed connection sort mode to {sortMode}.");
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

        var workbenchToastManager = ToastService.CurrentManager;
        var dialogViewModel = new ExportDialogViewModel(
            connection,
            SelectedDatabaseName,
            CurrentSchemaName,
            _lastDocumentExportDirectory,
            _databaseService);
        var exportRequest = await Ursa.Controls.Dialog.ShowCustomAsync<ExportDialog, ExportDialogViewModel, ExportDialogResultDto?>(
            dialogViewModel, MainWindow, new Ursa.Controls.DialogOptions { Title = "导出文档", CanResize = false });
        if (exportRequest == null)
        {
            return;
        }

        _lastDocumentExportDirectory = exportRequest.OutputDirectory;
        IsLoading = true;
        LoadingText = "正在准备导出文档...";

        try
        {
            var exportPayload = await _databaseExportPayloadService.BuildPayloadAsync(
                connection,
                exportRequest.SelectedObjects,
                progress => LoadingText = progress);
            if (!exportPayload.Success)
            {
                LoadingText = exportPayload.Message;
                ToastService.ShowError(exportPayload.Message, 5000);
                return;
            }

            if (exportPayload.TotalObjectCount == 0)
            {
                LoadingText = "没有可导出的数据。";
                ToastService.ShowWarning("没有可导出的数据。", 3000);
                return;
            }

            var exportFilePath = _databaseWorkbenchNamingService.BuildExportFilePath(exportRequest);
            var exported = exportRequest.DocumentType switch
            {
                ExportDocumentType.Excel => await _documentExportService.ExportToExcelAsync(
                    exportFilePath,
                    exportRequest.DocumentName,
                    exportPayload.Tables,
                    exportPayload.TableColumnsMap,
                    exportPayload.Views,
                    exportPayload.Procedures,
                    exportPayload.TableIndexesMap),
                ExportDocumentType.Markdown => await _documentExportService.ExportToMarkdownAsync(
                    exportFilePath,
                    exportRequest.DocumentName,
                    exportPayload.Tables,
                    exportPayload.TableColumnsMap,
                    exportPayload.Views,
                    exportPayload.Procedures,
                    exportPayload.TableIndexesMap),
                _ => false
            };

            if (!exported)
            {
                LoadingText = "文档导出失败，请查看日志获取详情。";
                ToastService.ShowError("文档导出失败，请查看日志获取详情。", 5000);
                return;
            }

            var exportedFileName = Path.GetFileName(exportFilePath);
            LoadingText = $"导出成功：{exportedFileName}";
            ToastService.ShowSuccess($"导出成功\n共 {exportPayload.TotalObjectCount} 个对象", 4000);
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

            var json = _connectionConfigurationService.SerializeConnections(Connections);
            await File.WriteAllTextAsync(file.Path.LocalPath, json);

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
            var payload = await _codeGenerationPayloadService.BuildPayloadAsync(
                connection,
                CurrentSchemaName,
                progress => LoadingText = progress);
            if (!payload.Success)
            {
                LoadingText = payload.Message;
                ToastService.ShowError(payload.Message, 5000);
                return;
            }

            if (payload.Tables.Count == 0)
            {
                LoadingText = "没有可生成代码的数据表。";
                ToastService.ShowWarning("没有可生成代码的数据表。", 3000);
                return;
            }

            var namespaceRoot = _databaseWorkbenchNamingService.BuildCodeGenerationNamespace(connection.Name, CurrentSchemaName);
            var generatedFileCount = 0;
            var failedItems = new List<string>();

            foreach (var table in payload.Tables.OrderBy(table => table.Name))
            {
                payload.TableColumnsMap.TryGetValue(table.Name, out var columns);
                columns ??= new List<ColumnModel>();

                if (generateEntities)
                {
                    LoadingText = $"正在生成实体类：{table.Name} ...";
                    var entityPath = Path.Combine(outputRootPath, "Entities", $"{_databaseWorkbenchNamingService.SanitizeCodeIdentifier(table.Name)}.cs");
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
                    var repositoryPath = Path.Combine(outputRootPath, "Repositories", $"{_databaseWorkbenchNamingService.SanitizeCodeIdentifier(table.Name)}Repository.cs");
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
                    var controllerPath = Path.Combine(outputRootPath, "Controllers", $"{_databaseWorkbenchNamingService.SanitizeCodeIdentifier(table.Name)}Controller.cs");
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
            var importedConnections = _connectionConfigurationService.DeserializeConnections(json);
            if (importedConnections == null || importedConnections.Count == 0)
            {
                ToastService.ShowWarning("所选文件中没有有效的连接配置。", 4000);
                return;
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

            var selectionResult = _databaseConnectionContextService.BuildDatabaseSelection(databases, preferredDatabase, success);
            foreach (var database in selectionResult.Databases)
            {
                AvailableDatabases.Add(database);
            }

            _suppressDatabaseSelectionChanged = true;
            SelectedDatabaseName = selectionResult.SelectedDatabase;
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

    private void RefreshFilteredAvailableDatabases()
    {
        var searchText = DatabaseSearchText.Trim();
        var filteredDatabases = string.IsNullOrWhiteSpace(searchText)
            ? AvailableDatabases
            : AvailableDatabases
                .Where(database => database.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        FilteredAvailableDatabases.Clear();
        foreach (var database in filteredDatabases)
        {
            FilteredAvailableDatabases.Add(database);
        }
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

    private async Task ShowErrorMessageAsync(string title, string message)
    {
        if (MainWindow == null)
        {
            return;
        }

        try
        {
            await Ursa.Controls.MessageBox.ShowAsync(
                MainWindow,
                message,
                title,
                icon: Ursa.Controls.MessageBoxIcon.Error,
                button: Ursa.Controls.MessageBoxButton.OK);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to show message box.", ex);
        }
    }

}
