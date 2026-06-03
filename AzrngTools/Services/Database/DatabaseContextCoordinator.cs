using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using AzrngTools.Models.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Services.Database;

public partial class DatabaseContextCoordinator : ObservableObject, IDatabaseContextCoordinator
{
    private readonly IDatabaseService _databaseService;
    private readonly IDatabaseConnectionContextService _databaseConnectionContextService;
    private readonly Func<ConnectionConfig?> _getSelectedConnection;
    private readonly DatabaseBrowserViewModel _browserViewModel;
    private readonly SqlQueryViewModel _sqlQueryViewModel;
    private bool _suppressDatabaseSelectionChanged;

    [ObservableProperty]
    private ObservableCollection<string> _availableDatabases = new();

    [ObservableProperty]
    private ObservableCollection<string> _filteredAvailableDatabases = new();

    [ObservableProperty]
    private string _databaseSearchText = string.Empty;

    [ObservableProperty]
    private string? _selectedDatabaseName;

    [ObservableProperty]
    private ConnectionConfig? _activeConnectionContext;

    [ObservableProperty]
    private ObservableCollection<SchemaModel> _schemas = new();

    [ObservableProperty]
    private string? _currentSchemaName;

    [ObservableProperty]
    private SchemaModel? _currentSchema;

    public bool HasAvailableDatabases => AvailableDatabases.Count > 0;

    public DatabaseContextCoordinator(
        IDatabaseService databaseService,
        IDatabaseConnectionContextService databaseConnectionContextService,
        Func<ConnectionConfig?> getSelectedConnection,
        DatabaseBrowserViewModel browserViewModel,
        SqlQueryViewModel sqlQueryViewModel)
    {
        _databaseService = databaseService;
        _databaseConnectionContextService = databaseConnectionContextService;
        _getSelectedConnection = getSelectedConnection;
        _browserViewModel = browserViewModel;
        _sqlQueryViewModel = sqlQueryViewModel;

        AvailableDatabases.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAvailableDatabases));
            RefreshFilteredAvailableDatabases();
        };
    }

    public async Task InitializeConnectionContextAsync(ConnectionConfig connection)
    {
        var preferredDatabase = string.IsNullOrWhiteSpace(connection.Database)
            ? null
            : connection.Database;

        await LoadAvailableDatabasesAsync(connection, preferredDatabase);

        if (_getSelectedConnection() != connection)
        {
            return;
        }

        var runtimeConnection = _databaseConnectionContextService.CreateRuntimeConnection(connection, SelectedDatabaseName);
        ActiveConnectionContext = runtimeConnection;
        await LoadConnectionContextAsync(runtimeConnection);
    }

    public void ResetConnectionContextState(ConnectionConfig? nextConnection)
    {
        ActiveConnectionContext = null;
        AvailableDatabases.Clear();

        _suppressDatabaseSelectionChanged = true;
        SelectedDatabaseName = null;
        DatabaseSearchText = string.Empty;
        _suppressDatabaseSelectionChanged = false;

        _sqlQueryViewModel.CurrentConnection = null;
        _browserViewModel.Reset();
    }

    public async Task SwitchDatabaseAsync()
    {
        var selectedConnection = _getSelectedConnection();
        if (selectedConnection == null)
        {
            return;
        }

        var runtimeConnection = _databaseConnectionContextService.CreateRuntimeConnection(selectedConnection, SelectedDatabaseName);
        ActiveConnectionContext = runtimeConnection;
        await LoadConnectionContextAsync(runtimeConnection);
    }

    private async Task LoadConnectionContextAsync(ConnectionConfig connection)
    {
        ActiveConnectionContext = connection;
        _sqlQueryViewModel.CurrentConnection = connection;
        _browserViewModel.CurrentConnection = connection;
        await LoadSchemasAsync(connection);
        await _browserViewModel.LoadDataAsync();

        if (_browserViewModel.FirstRootNode == null &&
            !string.IsNullOrWhiteSpace(_browserViewModel.LoadingText) &&
            (_browserViewModel.LoadingText.StartsWith("加载失败", StringComparison.Ordinal) ||
             _browserViewModel.LoadingText.StartsWith("加载异常", StringComparison.Ordinal)))
        {
            ToastService.ShowWarning($"对象浏览器未能加载：{_browserViewModel.LoadingText}", 4000);
        }
    }

    public async Task LoadSchemasAsync(ConnectionConfig config)
    {
        try
        {
            var result = await _databaseService.GetSchemasAsync(config);
            if (!result.IsSuccess)
            {
                LoggingService.LogError($"Failed to load schemas for {config.Name}: {result.Message}");
                return;
            }

            Schemas.Clear();
            var schemas = result.DataOrEmpty();
            foreach (var s in schemas) Schemas.Add(s);
            LoggingService.LogInfo($"Loaded {schemas.Count} schemas for {config.Name}.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Schema load failed for {config.Name}.", ex);
        }
    }

    public async Task SelectSchemaAsync(SchemaModel schema)
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
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to load the selected schema.", ex);
            ToastService.ShowError($"架构加载失败：{ex.Message}", 3000);
        }
    }

    public void ApplySchemaContext(string? schemaName)
    {
        CurrentSchemaName = schemaName;
        CurrentSchema = string.IsNullOrWhiteSpace(schemaName)
            ? null
            : Schemas.FirstOrDefault(schema => schema.Name == schemaName) ?? new SchemaModel { Name = schemaName };
    }

    public async Task LoadAvailableDatabasesAsync(ConnectionConfig connection, string? preferredDatabase)
    {
        AvailableDatabases.Clear();

        try
        {
            var result = await _databaseService.GetDatabaseNamesAsync(connection);
            if (_getSelectedConnection() != connection)
            {
                return;
            }

            var selectionResult = _databaseConnectionContextService.BuildDatabaseSelection(result.DataOrEmpty(), preferredDatabase, result.IsSuccess);
            foreach (var database in selectionResult.Databases)
            {
                AvailableDatabases.Add(database);
            }

            _suppressDatabaseSelectionChanged = true;
            SelectedDatabaseName = selectionResult.SelectedDatabase;
            DatabaseSearchText = selectionResult.SelectedDatabase ?? string.Empty;
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
                DatabaseSearchText = preferredDatabase;
                _suppressDatabaseSelectionChanged = false;
            }
        }
    }

    public void RefreshFilteredAvailableDatabases()
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

    public void ResetWorkspaceState()
    {
        Schemas.Clear();
        CurrentSchemaName = null;
        CurrentSchema = null;
    }

    public ConnectionConfig? GetActiveConnection()
    {
        return ActiveConnectionContext ?? _getSelectedConnection();
    }

    public bool ShouldSuppressDatabaseSelectionChanged()
    {
        return _suppressDatabaseSelectionChanged;
    }

    partial void OnDatabaseSearchTextChanged(string value)
    {
        RefreshFilteredAvailableDatabases();
    }
}
