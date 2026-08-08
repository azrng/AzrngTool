using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using AzrngTools.Views.Database.Workbench;
using DatabaseViewModel = AzrngTools.Models.Database.ViewModel;

namespace AzrngTools.ViewModels.Database;

public enum DetailWorkspaceMode
{
    Schema,
    Table,
    View,
    Procedure,
    SqlQuery
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IConnectionManagementCoordinator _connectionManager;
    private readonly IDatabaseContextCoordinator _databaseContextManager;
    private readonly IExportCoordinator _exportCoordinator;
    private readonly IPgDumpExportCoordinator _pgDumpExportCoordinator;

    public ObservableCollection<ConnectionConfig> Connections => _connectionManager.Connections;

    [ObservableProperty]
    private ConnectionConfig? _selectedConnection;

    public ConnectionConfig? ActiveConnectionContext => _databaseContextManager.ActiveConnectionContext;

    public ObservableCollection<string> AvailableDatabases => _databaseContextManager.AvailableDatabases;

    public ObservableCollection<string> FilteredAvailableDatabases => _databaseContextManager.FilteredAvailableDatabases;

    public string DatabaseSearchText
    {
        get => _databaseContextManager.DatabaseSearchText;
        set => _databaseContextManager.DatabaseSearchText = value;
    }

    public string? SelectedDatabaseName
    {
        get => _databaseContextManager.SelectedDatabaseName;
        set
        {
            if (_databaseContextManager.SelectedDatabaseName == value) return;
            _databaseContextManager.SelectedDatabaseName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentConnectionLabel));
            OnPropertyChanged(nameof(CompactConnectionLabel));
            DatabaseSearchText = value ?? string.Empty;

            if (_databaseContextManager.ShouldSuppressDatabaseSelectionChanged() ||
                SelectedConnection == null ||
                string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            RunConnectionContextTask(
                SwitchDatabaseWithResetAsync(value),
                $"切换数据库失败：{value}",
                "数据库切换失败");
        }
    }

    public ObservableCollection<ConnectionGroup> Groups => _connectionManager.Groups;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadingText;

    public ObservableCollection<SchemaModel> Schemas => _databaseContextManager.Schemas;

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

    public string? CurrentSchemaName
    {
        get => _databaseContextManager.CurrentSchemaName;
        set
        {
            var oldValue = _databaseContextManager.CurrentSchemaName;
            _databaseContextManager.ApplySchemaContext(value);
            if (oldValue != value)
            {
                OnPropertyChanged();
                SqlQueryViewModel.CurrentSchemaName = value;
            }
            OnPropertyChanged(nameof(CurrentSchema));
        }
    }

    public SchemaModel? CurrentSchema
    {
        get => _databaseContextManager.CurrentSchema;
        set
        {
            _databaseContextManager.CurrentSchema = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private bool _showOverviewPage = true;

    [ObservableProperty]
    private DetailWorkspaceMode _currentWorkspaceMode = DetailWorkspaceMode.Table;

    public bool ShowSchemaWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.Schema;

    public bool ShowTableWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.Table;

    public bool ShowViewWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.View;

    public bool ShowProcedureWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.Procedure;

    public bool ShowSqlQueryWorkspace => !ShowOverviewPage && CurrentWorkspaceMode == DetailWorkspaceMode.SqlQuery;

    public bool HasAvailableDatabases => _databaseContextManager.HasAvailableDatabases;

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

    /// <summary>
    /// 未显式注入数据库服务时复用的共享占位实例。
    /// 避免每次构造（如单元测试、设计时）都新建独立 <see cref="DatabaseService"/>，
    /// 保证桥接器缓存等内部状态在单进程内共享。
    /// </summary>
    private static readonly IDatabaseService SharedDatabaseService = new DatabaseService();

    public MainWindowViewModel()
        : this(
            SharedDatabaseService,
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
            null,
            null,
            null,
            null)
    {
    }

    public MainWindowViewModel(
        IDatabaseService databaseService,
        IConnectionConfigurationService? connectionConfigurationService = null,
        IConnectionGroupConfigurationService? connectionGroupConfigurationService = null,
        IDatabaseConnectionContextService? databaseConnectionContextService = null,
        IDocumentExportService? documentExportService = null,
        ICodeGenerationService? codeGenerationService = null,
        IDatabaseExportPayloadService? databaseExportPayloadService = null,
        ICodeGenerationPayloadService? codeGenerationPayloadService = null,
        IDatabaseWorkbenchNamingService? databaseWorkbenchNamingService = null,
        IConnectionManagementCoordinator? connectionManager = null,
        IDatabaseContextCoordinator? databaseContextManager = null,
        IExportCoordinator? exportCoordinator = null,
        DatabaseBrowserViewModel? browserViewModel = null,
        TableDetailViewModel? tableDetailViewModel = null,
        ViewDetailViewModel? viewDetailViewModel = null,
        StoredProcedureDetailViewModel? storedProcedureDetailViewModel = null,
        SqlQueryViewModel? sqlQueryViewModel = null)
    {
        _databaseService = databaseService;
        var resolvedConnectionConfigService = connectionConfigurationService ?? new ConnectionConfigurationService();
        var resolvedGroupConfigService = connectionGroupConfigurationService ?? new ConnectionGroupConfigurationService();
        var resolvedConnectionContextService = databaseConnectionContextService ?? new DatabaseConnectionContextService();
        _connectionManager = connectionManager ?? new ConnectionManagementCoordinator(
            resolvedConnectionConfigService,
            resolvedGroupConfigService,
            resolvedConnectionContextService);
        BrowserViewModel = browserViewModel ?? new DatabaseBrowserViewModel(databaseService);
        TableDetailViewModel = tableDetailViewModel ?? new TableDetailViewModel(databaseService);
        ViewDetailViewModel = viewDetailViewModel ?? new ViewDetailViewModel(databaseService);
        StoredProcedureDetailViewModel = storedProcedureDetailViewModel ?? new StoredProcedureDetailViewModel(databaseService);
        SqlQueryViewModel = sqlQueryViewModel ?? new SqlQueryViewModel(databaseService);
        // 注入危险 SQL 二次确认弹窗，避免 ViewModel 直接依赖 Window
        SqlQueryViewModel.ConfirmDangerousSqlAsync = ConfirmDangerousSqlExecutionAsync;
        _databaseContextManager = databaseContextManager ?? new DatabaseContextCoordinator(
            _databaseService,
            resolvedConnectionContextService,
            () => SelectedConnection,
            BrowserViewModel,
            SqlQueryViewModel);
        _exportCoordinator = exportCoordinator ?? new ExportCoordinator(
            _databaseService,
            documentExportService,
            codeGenerationService,
            databaseExportPayloadService,
            codeGenerationPayloadService,
            databaseWorkbenchNamingService);
        _pgDumpExportCoordinator = new PgDumpExportCoordinator(_databaseService);

        _databaseContextManager.PropertyChanged += OnDatabaseContextManagerPropertyChanged;
    }

    private void OnDatabaseContextManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IDatabaseContextCoordinator.SelectedDatabaseName):
                OnPropertyChanged(nameof(SelectedDatabaseName));
                OnPropertyChanged(nameof(CurrentConnectionLabel));
                OnPropertyChanged(nameof(CompactConnectionLabel));
                OnPropertyChanged(nameof(DatabaseSearchText));
                break;
            case nameof(IDatabaseContextCoordinator.DatabaseSearchText):
                OnPropertyChanged(nameof(DatabaseSearchText));
                break;
            case nameof(IDatabaseContextCoordinator.FilteredAvailableDatabases):
                OnPropertyChanged(nameof(FilteredAvailableDatabases));
                break;
            case nameof(IDatabaseContextCoordinator.HasAvailableDatabases):
                OnPropertyChanged(nameof(HasAvailableDatabases));
                break;
            case nameof(IDatabaseContextCoordinator.ActiveConnectionContext):
                OnPropertyChanged(nameof(ActiveConnectionContext));
                break;
            case nameof(IDatabaseContextCoordinator.CurrentSchemaName):
                OnPropertyChanged(nameof(CurrentSchemaName));
                OnPropertyChanged(nameof(CurrentSchema));
                SqlQueryViewModel.CurrentSchemaName = _databaseContextManager.CurrentSchemaName;
                break;
        }
    }

    private static string GetAppVersion()
    {
        return typeof(global::AzrngTools.App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
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

            var vm = new ConnectionDialogViewModel(Connections, _connectionManager.SaveConnections, SelectedConnection, _databaseService);
            var result = await Ursa.Controls.OverlayDrawer.ShowCustomAsync<ConnectionDialog, ConnectionDialogViewModel, ConnectionConfig?>(
                vm, "wbDrawer", new Ursa.Controls.Options.DrawerOptions
                {
                    Position = Ursa.Common.Position.Left,
                    MinWidth = 480,
                    IsCloseButtonVisible = true,
                    CanLightDismiss = true
                });
            if (result == null)
            {
                return;
            }

            result.UpdateUsageStats();
            var targetConnection = _connectionManager.ResolveConnection(result);
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

        _connectionManager.AddConnection(connection);
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
            _connectionManager.DeleteConnection(SelectedConnection);
            SelectedConnection = null;

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

            var result = await _databaseService.TestConnectionAsync(connection);
            if (result.IsSuccess)
            {
                LoadingText = $"连接测试成功：{connection.Name}";
                LoggingService.LogOperation($"连接测试成功：{connection.Name}");
                await InitializeConnectionContextAsync(connection);
                ToastService.ShowSuccess($"连接成功：{connection.Name}\n{result.Message}", 3000);
                return;
            }

            LoadingText = $"连接测试失败：{connection.Name}";
            LoggingService.LogError($"连接测试失败：{connection.Name} - {result.Message}");

            var suggestion = result.Data?.Suggestion;
            var fullMessage = string.IsNullOrWhiteSpace(suggestion)
                ? result.Message
                : $"{result.Message}\n\n建议：\n{suggestion}";

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
        _databaseContextManager.ResetConnectionContextState(nextConnection);
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

    partial void OnShowOverviewPageChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSchemaWorkspace));
        OnPropertyChanged(nameof(ShowTableWorkspace));
        OnPropertyChanged(nameof(ShowViewWorkspace));
        OnPropertyChanged(nameof(ShowProcedureWorkspace));
        OnPropertyChanged(nameof(ShowSqlQueryWorkspace));
    }

    partial void OnCurrentWorkspaceModeChanged(DetailWorkspaceMode value)
    {
        OnPropertyChanged(nameof(ShowSchemaWorkspace));
        OnPropertyChanged(nameof(ShowTableWorkspace));
        OnPropertyChanged(nameof(ShowViewWorkspace));
        OnPropertyChanged(nameof(ShowProcedureWorkspace));
        OnPropertyChanged(nameof(ShowSqlQueryWorkspace));
    }

    [RelayCommand]
    private async Task RefreshBrowserAsync()
    {
        var connection = _databaseContextManager.GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        BrowserViewModel.CurrentConnection = connection;
        await BrowserViewModel.LoadDataAsync();
        ToastService.ShowInfo($"已刷新对象树：{connection.Name} / {connection.Database}", 2000);
    }

    private async Task SwitchDatabaseWithResetAsync(string databaseName)
    {
        ResetWorkspaceState();

        await _databaseContextManager.SwitchDatabaseAsync();

        var activeConnection = _databaseContextManager.ActiveConnectionContext;
        if (activeConnection != null)
        {
            TableDetailViewModel.CurrentConnection = activeConnection;
            ViewDetailViewModel.CurrentConnection = activeConnection;
            StoredProcedureDetailViewModel.CurrentConnection = activeConnection;
        }
    }

    private async Task InitializeConnectionContextAsync(ConnectionConfig connection)
    {
        await _databaseContextManager.InitializeConnectionContextAsync(connection);

        var activeConnection = _databaseContextManager.ActiveConnectionContext;
        if (activeConnection != null)
        {
            TableDetailViewModel.CurrentConnection = activeConnection;
            ViewDetailViewModel.CurrentConnection = activeConnection;
            StoredProcedureDetailViewModel.CurrentConnection = activeConnection;
        }
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

    public async Task ActivateSchemaAsync(SchemaModel schema)
    {
        if (schema == null)
        {
            return;
        }

        var connection = _databaseContextManager.GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        CurrentSchemaName = schema.Name;
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
        CurrentWorkspaceMode = DetailWorkspaceMode.Table;
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

        TableDetailViewModel.ShowObjectList = true;
        ViewDetailViewModel.ShowObjectList = true;
        StoredProcedureDetailViewModel.ShowObjectList = true;

        switch (CurrentWorkspaceMode)
        {
            case DetailWorkspaceMode.View:
                ViewDetailViewModel.SelectedView = null;
                break;
            case DetailWorkspaceMode.Procedure:
                StoredProcedureDetailViewModel.SelectedProcedure = null;
                break;
            default:
                TableDetailViewModel.SelectedTable = null;
                break;
        }
    }

    [RelayCommand]
    private void ActivateSqlQuery()
    {
        var connection = _databaseContextManager.GetActiveConnection();
        if (connection == null)
        {
            ToastService.ShowWarning("请先选择数据库连接。", 2000);
            return;
        }

        SqlQueryViewModel.CurrentConnection = connection;
        ShowOverviewPage = false;
        CurrentWorkspaceMode = DetailWorkspaceMode.SqlQuery;
    }

    public async Task ActivateTableAsync(TableModel table)
    {
        if (table == null)
        {
            return;
        }

        var connection = _databaseContextManager.GetActiveConnection();
        if (connection == null)
        {
            return;
        }

        CurrentSchemaName = table.Schema;
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

        var connection = _databaseContextManager.GetActiveConnection();
        if (connection == null)
        {
            return;
        }

        CurrentSchemaName = view.Schema;
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

        var connection = _databaseContextManager.GetActiveConnection();
        if (connection == null)
        {
            return;
        }

        CurrentSchemaName = procedure.Schema;
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

    [RelayCommand]
    private void ChangeSort(string sortMode)
    {
        _connectionManager.ChangeSort(sortMode);
    }

    [RelayCommand]
    private async Task OpenExportDialogAsync()
    {
        var connection = _databaseContextManager.GetActiveConnection();
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

        await _exportCoordinator.OpenExportDialogAsync(MainWindow, connection, SelectedDatabaseName, CurrentSchemaName,
            setLoading: value => IsLoading = value,
            setLoadingText: value => LoadingText = value);
    }

    [RelayCommand]
    private async Task OpenPgDumpDialogAsync()
    {
        var connection = SelectedConnection ?? _databaseContextManager.GetActiveConnection();
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

        await _pgDumpExportCoordinator.OpenPgDumpDialogAsync(MainWindow, connection, SelectedDatabaseName,
            setLoading: value => IsLoading = value,
            setLoadingText: value => LoadingText = value);
    }

    [RelayCommand]
    private async Task ExportConnectionsAsync()
    {
        await _connectionManager.ExportConnectionsAsync(MainWindow);
    }

    [RelayCommand]
    private async Task GenerateCodeAsync(string? mode)
    {
        var connection = _databaseContextManager.GetActiveConnection();
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

        await _exportCoordinator.GenerateCodeAsync(MainWindow, connection, CurrentSchemaName, mode,
            setLoading: value => IsLoading = value,
            setLoadingText: value => LoadingText = value);
    }

    [RelayCommand]
    private async Task ImportConnectionsAsync()
    {
        await _connectionManager.ImportConnectionsAsync(MainWindow);
    }

    [RelayCommand]
    private void AddGroup(string groupName)
    {
        _connectionManager.AddGroup(groupName);
    }

    [RelayCommand]
    private void DeleteGroup(string groupId)
    {
        _connectionManager.DeleteGroup(groupId);
    }

    [RelayCommand]
    private void ChangeGroupFilter(string? groupId)
    {
        _connectionManager.ChangeGroupFilter(groupId);
    }

    [RelayCommand]
    private async Task SelectSchemaAsync(SchemaModel schema)
    {
        if (schema == null)
        {
            ToastService.ShowWarning("请先选择架构。", 2000);
            return;
        }

        var connection = _databaseContextManager.GetActiveConnection();
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

    /// <summary>
    /// SQL 工作台执行危险语句（DROP/TRUNCATE/DELETE）前的二次确认。
    /// 由 <see cref="SqlQueryViewModel.ConfirmDangerousSqlAsync"/> 调用。
    /// </summary>
    private async Task<bool> ConfirmDangerousSqlExecutionAsync(string dangerDescription)
    {
        if (MainWindow == null)
        {
            return false;
        }

        try
        {
            var result = await Ursa.Controls.MessageBox.ShowAsync(
                MainWindow,
                $"{dangerDescription}{Environment.NewLine}{Environment.NewLine}此操作无法撤销。",
                "确认执行危险操作",
                icon: Ursa.Controls.MessageBoxIcon.Warning,
                button: Ursa.Controls.MessageBoxButton.YesNo);
            return result == Ursa.Controls.MessageBoxResult.Yes;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to show dangerous SQL confirmation.", ex);
            return false;
        }
    }

}
