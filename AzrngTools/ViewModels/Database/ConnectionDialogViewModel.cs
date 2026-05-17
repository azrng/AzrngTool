using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using Irihi.Avalonia.Shared.Contracts;

namespace AzrngTools.ViewModels.Database;

public class DatabaseTypeCard
{
    public DatabaseType Type { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDisabled => !IsEnabled;
    public string Hint { get; set; } = string.Empty;
}

public partial class ConnectionDialogViewModel : ViewModelBase, IDialogContext
{
    private const string NameFieldKey = nameof(ConnectionConfig.Name);
    private const string HostFieldKey = nameof(ConnectionConfig.Host);
    private const string PortFieldKey = nameof(ConnectionConfig.Port);
    private const string UsernameFieldKey = nameof(ConnectionConfig.Username);
    private const string PasswordFieldKey = nameof(ConnectionConfig.Password);
    private const string DatabaseFieldKey = nameof(ConnectionConfig.Database);

    private readonly DatabaseService _databaseService;
    private readonly ObservableCollection<ConnectionConfig> _savedConnections;
    private readonly Action? _persistConnections;
    private readonly Dictionary<string, string> _fieldValidationErrors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _touchedFields = new(StringComparer.Ordinal);
    private ConnectionConfig? _trackedConnectionConfig;
    private bool _showAllValidationFeedback;

    [ObservableProperty]
    private ObservableCollection<DatabaseTypeCard> _databaseTypeCards = new();

    [ObservableProperty]
    private ObservableCollection<ConnectionConfig> _filteredConnections = new();

    [ObservableProperty]
    private ConnectionConfig? _selectedSavedConnection;

    [ObservableProperty]
    private ConnectionConfig _connectionConfig = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _connectionStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isConnectionSuccess;

    [ObservableProperty]
    private ObservableCollection<string> _availableDatabases = new();

    [ObservableProperty]
    private ObservableCollection<string> _validationErrors = new();

    [ObservableProperty]
    private bool _showDatabaseTypeSelector;

    [ObservableProperty]
    private bool _isCreatingNewConnection;

    public ConnectionConfig? DialogResultConnection { get; private set; }

    public bool HasAvailableDatabases => AvailableDatabases.Count > 0;

    public bool HasValidationErrors => ValidationErrors.Count > 0;

    public bool HasFilteredConnections => FilteredConnections.Count > 0;

    public bool ShowEditor => !ShowDatabaseTypeSelector;

    public bool ShowBackButton => ShowEditor && IsCreatingNewConnection;

    public bool IsSQLiteSelected => ConnectionConfig?.DatabaseType == DatabaseType.Sqlite;

    public bool IsSqlServerSelected => ConnectionConfig?.DatabaseType == DatabaseType.SqlServer;

    public bool IsPostgreSqlSelected => ConnectionConfig?.DatabaseType == DatabaseType.PostgresSql;

    public bool IsMySqlSelected => ConnectionConfig?.DatabaseType == DatabaseType.MySql;

    public bool ShowServerFields => !IsSQLiteSelected;

    public bool ShowCredentialFields => !IsSQLiteSelected && !ConnectionConfig.UseWindowsAuthentication;

    public bool ShowUsernameField => !IsSQLiteSelected && !ConnectionConfig.UseWindowsAuthentication;

    public bool ShowDatabaseSelector => !IsSQLiteSelected;

    public bool ShowDatabaseFilePathInput => IsSQLiteSelected;

    public bool ShowRefreshDatabaseButton => !IsSQLiteSelected;

    public bool ShowTestConnectionButton => !IsSQLiteSelected;

    public bool ShowTestButton => ShowEditor && ShowTestConnectionButton;

    public bool ShowWindowsAuthenticationOption => IsSqlServerSelected;

    public bool IsNameInvalid => HasFieldValidationError(NameFieldKey);

    public bool IsHostInvalid => HasFieldValidationError(HostFieldKey);

    public bool IsPortInvalid => HasFieldValidationError(PortFieldKey);

    public bool IsUsernameInvalid => HasFieldValidationError(UsernameFieldKey);

    public bool IsPasswordInvalid => HasFieldValidationError(PasswordFieldKey);

    public bool IsDatabaseInvalid => HasFieldValidationError(DatabaseFieldKey);

    public string NameValidationMessage => GetFieldValidationMessage(NameFieldKey);

    public string HostValidationMessage => GetFieldValidationMessage(HostFieldKey);

    public string PortValidationMessage => GetFieldValidationMessage(PortFieldKey);

    public string UsernameValidationMessage => GetFieldValidationMessage(UsernameFieldKey);

    public string PasswordValidationMessage => GetFieldValidationMessage(PasswordFieldKey);

    public string DatabaseValidationMessage => GetFieldValidationMessage(DatabaseFieldKey);

    public bool IsDatabaseSelectionEnabled => !IsConnecting;

    public string DatabaseSelectionHint => IsConnecting
        ? "正在加载数据库列表..."
        : "点击刷新自动获取数据库列表，也可以直接输入数据库名称。";

    public string DatabaseFilePathHint => "请输入 Sqlite 数据库文件路径";

    public string SQLiteModeHint => "Sqlite 连接无需测试，选择数据库文件后直接保存即可";

    public string SaveButtonText => "保存";

    public string CurrentBrandMonogram => ConnectionConfig?.DatabaseType switch
    {
        DatabaseType.PostgresSql => "PG",
        DatabaseType.MySql => "MY",
        DatabaseType.SqlServer => "MS",
        DatabaseType.Sqlite => "SQ",
        DatabaseType.Oracle => "OR",
        _ => "DB"
    };

    public string CurrentBrandWordmark => ConnectionConfig?.DatabaseType switch
    {
        DatabaseType.PostgresSql => "PostgresSql",
        DatabaseType.MySql => "MySql",
        DatabaseType.SqlServer => "SQL Server",
        DatabaseType.Sqlite => "Sqlite",
        DatabaseType.Oracle => "Oracle",
        _ => "Database"
    };

    public string CurrentEditorTitle => ConnectionConfig?.DatabaseType switch
    {
        DatabaseType.PostgresSql => "PostgresSql 连接设置",
        DatabaseType.MySql => "MySql 连接设置",
        DatabaseType.SqlServer => "SQL Server 连接设置",
        DatabaseType.Sqlite => "Sqlite 连接设置",
        DatabaseType.Oracle => "Oracle 连接设置",
        _ => "数据库连接设置"
    };

    public string CurrentEditorDescription => ConnectionConfig?.DatabaseType switch
    {
        DatabaseType.PostgresSql => "适合多 Schema、结构化管理场景，默认端口 5432。",
        DatabaseType.MySql => "适合业务系统快速接入场景，默认端口 3306。",
        DatabaseType.SqlServer => "当前版本暂未开放新建。",
        DatabaseType.Sqlite => "当前版本暂未开放新建。",
        DatabaseType.Oracle => "当前版本暂未开放新建。",
        _ => "请填写连接信息并保存。"
    };

    public string EditorTitle => ShowDatabaseTypeSelector
        ? "选择数据库类型"
        : $"{GetDatabaseTypeDisplayName(ConnectionConfig.DatabaseType)} 连接设置";

    public string EditorSubtitle => ShowDatabaseTypeSelector
        ? "在右侧选择数据库类型后开始创建连接"
        : IsCreatingNewConnection
            ? "填写连接信息并保存到左侧列表"
            : "点击左侧已保存连接即可查看并编辑详细配置";

    public string EmptyConnectionsMessage => string.IsNullOrWhiteSpace(SearchText)
        ? "暂无已保存连接，点击右上角 + 新建连接"
        : "没有匹配的连接";

    public ConnectionDialogViewModel(
        ObservableCollection<ConnectionConfig>? savedConnections = null,
        Action? persistConnections = null,
        ConnectionConfig? selectedConnection = null)
    {
        _databaseService = new DatabaseService();
        _savedConnections = savedConnections ?? new ObservableCollection<ConnectionConfig>();
        _persistConnections = persistConnections;

        _savedConnections.CollectionChanged += OnSavedConnectionsChanged;
        AvailableDatabases.CollectionChanged += OnAvailableDatabasesChanged;

        // 初始化数据库类型卡片
        InitializeDatabaseTypeCards();

        ConnectionConfig = CreateDefaultConnectionConfig();
        RefreshFilteredConnections();

        if (selectedConnection != null && _savedConnections.Contains(selectedConnection))
        {
            SelectedSavedConnection = selectedConnection;
        }
        else if (_savedConnections.Count > 0)
        {
            SelectedSavedConnection = _savedConnections[0];
        }
        else
        {
            BeginCreateNewConnection();
        }
    }

    private void InitializeDatabaseTypeCards()
    {
        DatabaseTypeCards = new ObservableCollection<DatabaseTypeCard>
        {
            new() { Type = DatabaseType.PostgresSql, Icon = "PG", Name = "PostgresSql", Hint = "可配置" },
            new() { Type = DatabaseType.MySql, Icon = "MY", Name = "MySql", Hint = "可配置" },
            new() { Type = DatabaseType.SqlServer, Icon = "MS", Name = "SQL Server", IsEnabled = false, Hint = "待接入" },
            new() { Type = DatabaseType.Sqlite, Icon = "SQ", Name = "Sqlite", IsEnabled = false, Hint = "待接入" },
            new() { Type = DatabaseType.Oracle, Icon = "OR", Name = "Oracle", IsEnabled = false, Hint = "待接入" }
        };
    }

    partial void OnConnectionConfigChanged(ConnectionConfig value)
    {
        if (_trackedConnectionConfig != null)
        {
            _trackedConnectionConfig.PropertyChanged -= OnConnectionConfigPropertyChanged;
        }

        _trackedConnectionConfig = value;

        if (_trackedConnectionConfig != null)
        {
            _trackedConnectionConfig.PropertyChanged += OnConnectionConfigPropertyChanged;
        }

        AvailableDatabases.Clear();
        NotifyEditorStateChanged();
        SaveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
        RefreshDatabasesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSavedConnectionChanged(ConnectionConfig? value)
    {
        DeleteConnectionCommand.NotifyCanExecuteChanged();

        if (value == null)
        {
            OnPropertyChanged(nameof(ShowBackButton));
            return;
        }

        IsCreatingNewConnection = false;
        ShowDatabaseTypeSelector = false;
        ConnectionConfig = CloneConnection(value);
        ResetValidationState(clearStatus: true);
        PopulateAvailableDatabasesFromCurrentConfig();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredConnections();
        OnPropertyChanged(nameof(EmptyConnectionsMessage));
    }

    partial void OnShowDatabaseTypeSelectorChanged(bool value)
    {
        NotifyEditorStateChanged();
        SaveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCreatingNewConnectionChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorSubtitle));
        OnPropertyChanged(nameof(ShowBackButton));
    }

    partial void OnIsConnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDatabaseSelectionEnabled));
        OnPropertyChanged(nameof(DatabaseSelectionHint));
        SaveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
        RefreshDatabasesCommand.NotifyCanExecuteChanged();
    }

    private void OnConnectionConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var fieldKey = GetFieldKey(e.PropertyName);
        if (fieldKey != null)
        {
            _touchedFields.Add(fieldKey);
        }

        if (e.PropertyName == nameof(ConnectionConfig.DatabaseType))
        {
            NotifyDatabaseTypeStateChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }

        if (e.PropertyName == nameof(ConnectionConfig.UseWindowsAuthentication))
        {
            OnPropertyChanged(nameof(ShowCredentialFields));
        }

        if (_showAllValidationFeedback || _touchedFields.Count > 0)
        {
            ValidateForm();
        }

        SaveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
        RefreshDatabasesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CreateNewConnection()
    {
        BeginCreateNewConnection();
    }

    [RelayCommand]
    private void BackToTypeSelector()
    {
        ShowDatabaseTypeSelector = true;
        ResetValidationState(clearStatus: true);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteConnection))]
    private void DeleteConnection()
    {
        if (SelectedSavedConnection == null)
        {
            return;
        }

        var removedConnection = SelectedSavedConnection;
        _savedConnections.Remove(removedConnection);
        PersistConnections();
        RefreshFilteredConnections();
        ResetValidationState(clearStatus: true);
        ConnectionStatusMessage = string.Empty;
        ToastService.ShowSuccess($"已删除连接：{removedConnection.Name}", autoCloseDelay: 2000);

        if (FilteredConnections.Count > 0)
        {
            SelectedSavedConnection = FilteredConnections[0];
            return;
        }

        if (_savedConnections.Count > 0)
        {
            SelectedSavedConnection = _savedConnections[0];
            return;
        }

        BeginCreateNewConnection();
    }

    private bool CanDeleteConnection()
    {
        return SelectedSavedConnection != null;
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        if (!ValidateForm(showAllErrors: true, showToast: true))
        {
            return;
        }

        IsConnecting = true;
        ConnectionStatusMessage = "正在连接...";
        IsConnectionSuccess = false;

        try
        {
            var (success, message, suggestion) = await _databaseService.TestConnectionAsync(ConnectionConfig);

            IsConnectionSuccess = success;

            if (success)
            {
                LoggingService.LogOperation($"测试连接成功：{ConnectionConfig.Name}");
                ToastService.ShowSuccess("连接测试成功", autoCloseDelay: 2000);
            }
            else
            {
                var fullMessage = suggestion != null
                    ? $"{message}\n\n{suggestion}"
                    : message;
                LoggingService.LogError($"测试连接失败: {ConnectionConfig.Name} - {message}");
                ToastService.ShowError(fullMessage, autoCloseDelay: 5000);
            }
        }
        catch (Exception ex)
        {
            IsConnectionSuccess = false;
            ToastService.ShowError($"连接失败: {ex.Message}", autoCloseDelay: 5000);
            LoggingService.LogError($"测试连接异常: {ConnectionConfig.Name}", ex);
        }
        finally
        {
            IsConnecting = false;
            ConnectionStatusMessage = string.Empty;
        }
    }
    private bool CanTestConnection()
    {
        return ShowEditor &&
               !IsSQLiteSelected &&
               !IsConnecting &&
               !string.IsNullOrWhiteSpace(ConnectionConfig?.Host);
    }

    [RelayCommand(CanExecute = nameof(CanRefreshDatabases))]
    private async Task RefreshDatabasesAsync()
    {
        var refreshValidationMessage = GetRefreshDatabaseValidationMessage();
        if (!string.IsNullOrWhiteSpace(refreshValidationMessage))
        {
            IsConnectionSuccess = false;
            ToastService.ShowWarning(refreshValidationMessage, autoCloseDelay: 3500);
            return;
        }

        IsConnecting = true;
        ConnectionStatusMessage = "正在加载数据库列表...";

        try
        {
            var currentDatabase = ConnectionConfig.Database;
            var (success, databases, message) = await _databaseService.GetDatabaseNamesAsync(ConnectionConfig);
            if (!success)
            {
                IsConnectionSuccess = false;
                ToastService.ShowError(message, autoCloseDelay: 5000);
                return;
            }

            AvailableDatabases.Clear();
            foreach (var database in databases)
            {
                AvailableDatabases.Add(database);
            }

            if (!string.IsNullOrWhiteSpace(currentDatabase))
            {
                if (!AvailableDatabases.Contains(currentDatabase))
                {
                    AvailableDatabases.Insert(0, currentDatabase);
                }

                ConnectionConfig.Database = currentDatabase;
            }
            else if (AvailableDatabases.Count > 0)
            {
                ConnectionConfig.Database = AvailableDatabases.First();
            }

            IsConnectionSuccess = true;
        }
        catch (Exception ex)
        {
            IsConnectionSuccess = false;
            ToastService.ShowError($"加载数据库列表失败: {ex.Message}", autoCloseDelay: 5000);
        }
        finally
        {
            IsConnecting = false;
            ConnectionStatusMessage = string.Empty;
        }
    }

    private bool CanRefreshDatabases()
    {
        return ShowEditor &&
               !IsSQLiteSelected &&
               !IsConnecting;
    }

    private string? GetRefreshDatabaseValidationMessage()
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(ConnectionConfig?.Host))
        {
            missingFields.Add("主机地址");
        }

        if (ConnectionConfig?.Port <= 0)
        {
            missingFields.Add("端口");
        }

        if (ConnectionConfig is { UseWindowsAuthentication: false })
        {
            if (string.IsNullOrWhiteSpace(ConnectionConfig.Username))
            {
                missingFields.Add("用户名");
            }

            if (string.IsNullOrWhiteSpace(ConnectionConfig.Password))
            {
                missingFields.Add("密码");
            }
        }

        if (missingFields.Count == 0)
        {
            return null;
        }

        return $"请先填写{string.Join('、', missingFields)}后再刷新数据库列表。";
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        SaveCurrentConnection();
    }

    private bool CanSave()
    {
        return ShowEditor &&
               !IsConnecting &&
               !string.IsNullOrWhiteSpace(ConnectionConfig?.Name);
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        var savedConnection = SaveCurrentConnection();
        if (savedConnection == null)
        {
            return;
        }

        DialogResultConnection = savedConnection;
        OnCloseRequested(true);
    }

    private bool CanConnect()
    {
        return CanSave();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultConnection = null;
        OnCloseRequested(false);
    }

    [RelayCommand]
    private void SelectDatabaseType(DatabaseType dbType)
    {
        if (!IsDatabaseTypeEnabled(dbType))
        {
            ToastService.ShowWarning($"{GetDatabaseTypeDisplayName(dbType)} 暂未开放，请先使用 PostgresSql 或 MySql。", autoCloseDelay: 3000);
            IsConnectionSuccess = false;
            return;
        }

        ConnectionConfig.DatabaseType = dbType;
        ConnectionConfig.Port = dbType switch
        {
            DatabaseType.SqlServer => 1433,
            DatabaseType.MySql => 3306,
            DatabaseType.PostgresSql => 5432,
            DatabaseType.Oracle => 1521,
            DatabaseType.Sqlite => 0,
            DatabaseType.Dm => 5236,
            _ => 1433
        };

        ConnectionConfig.Database = string.Empty;
        ShowDatabaseTypeSelector = false;
        ResetValidationState(clearStatus: true);
    }

    private ConnectionConfig? SaveCurrentConnection()
    {
        if (!ValidateForm(showAllErrors: true, showToast: true))
        {
            return null;
        }

        // 检查连接名称唯一性
        if (!ValidateConnectionNameUnique())
        {
            return null;
        }

        SearchText = string.Empty;

        if (IsCreatingNewConnection || SelectedSavedConnection == null)
        {
            var newConnection = CloneConnection(ConnectionConfig);
            _savedConnections.Add(newConnection);
            PersistConnections();
            RefreshFilteredConnections();
            SelectedSavedConnection = newConnection;
            IsCreatingNewConnection = false;
            ToastService.ShowSuccess($"连接已保存：{newConnection.Name}", autoCloseDelay: 2000);
            return newConnection;
        }

        CopyConnection(ConnectionConfig, SelectedSavedConnection);
        PersistConnections();
        RefreshFilteredConnections();
        ConnectionConfig = CloneConnection(SelectedSavedConnection);
        PopulateAvailableDatabasesFromCurrentConfig();
        ToastService.ShowSuccess($"连接已保存：{SelectedSavedConnection.Name}", autoCloseDelay: 2000);
        return SelectedSavedConnection;
    }

    private void BeginCreateNewConnection()
    {
        IsCreatingNewConnection = true;
        ShowDatabaseTypeSelector = true;
        DialogResultConnection = null;
        SelectedSavedConnection = null;
        ConnectionConfig = CreateDefaultConnectionConfig();
        ResetValidationState(clearStatus: true);
    }

    private void RefreshFilteredConnections()
    {
        var keyword = SearchText?.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _savedConnections.ToList()
            : _savedConnections.Where(connection =>
                    (connection.Name ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (connection.Host ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (connection.Database ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    connection.DatabaseType.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        FilteredConnections.Clear();
        foreach (var c in filtered) FilteredConnections.Add(c);

        if (SelectedSavedConnection != null && !FilteredConnections.Contains(SelectedSavedConnection))
        {
            SelectedSavedConnection = FilteredConnections.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HasFilteredConnections));
        OnPropertyChanged(nameof(EmptyConnectionsMessage));
    }

    private void ResetValidationState(bool clearStatus)
    {
        _showAllValidationFeedback = false;
        _touchedFields.Clear();
        _fieldValidationErrors.Clear();
        ValidationErrors.Clear();
        OnPropertyChanged(nameof(HasValidationErrors));
        NotifyAllFieldValidationStateChanged();

        if (clearStatus)
        {
            ConnectionStatusMessage = string.Empty;
            IsConnectionSuccess = false;
        }
    }

    private void PopulateAvailableDatabasesFromCurrentConfig()
    {
        AvailableDatabases.Clear();

        if (!string.IsNullOrWhiteSpace(ConnectionConfig.Database))
        {
            AvailableDatabases.Add(ConnectionConfig.Database);
        }
    }

    private void OnSavedConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasFilteredConnections));
        OnPropertyChanged(nameof(EmptyConnectionsMessage));
        DeleteConnectionCommand.NotifyCanExecuteChanged();
    }

    private void OnAvailableDatabasesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAvailableDatabases));
        OnPropertyChanged(nameof(IsDatabaseSelectionEnabled));
    }

    private void NotifyEditorStateChanged()
    {
        NotifyDatabaseTypeStateChanged();
        OnPropertyChanged(nameof(ShowEditor));
        OnPropertyChanged(nameof(ShowBackButton));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSubtitle));
        OnPropertyChanged(nameof(ShowTestButton));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private void NotifyDatabaseTypeStateChanged()
    {
        OnPropertyChanged(nameof(IsSQLiteSelected));
        OnPropertyChanged(nameof(ShowServerFields));
        OnPropertyChanged(nameof(ShowCredentialFields));
        OnPropertyChanged(nameof(ShowDatabaseSelector));
        OnPropertyChanged(nameof(ShowDatabaseFilePathInput));
        OnPropertyChanged(nameof(ShowRefreshDatabaseButton));
        OnPropertyChanged(nameof(ShowTestConnectionButton));
        OnPropertyChanged(nameof(ShowTestButton));
        OnPropertyChanged(nameof(SQLiteModeHint));
        OnPropertyChanged(nameof(DatabaseFilePathHint));
        OnPropertyChanged(nameof(ShowWindowsAuthenticationOption));
        OnPropertyChanged(nameof(IsSqlServerSelected));
        OnPropertyChanged(nameof(IsPostgreSqlSelected));
        OnPropertyChanged(nameof(IsMySqlSelected));
        OnPropertyChanged(nameof(ShowUsernameField));
        OnPropertyChanged(nameof(ShowDatabaseSelector));
        OnPropertyChanged(nameof(CurrentBrandMonogram));
        OnPropertyChanged(nameof(CurrentBrandWordmark));
        OnPropertyChanged(nameof(CurrentEditorTitle));
        OnPropertyChanged(nameof(CurrentEditorDescription));
        NotifyAllFieldValidationStateChanged();
    }

    private bool ValidateForm(bool showAllErrors = false, bool showToast = false)
    {
        if (showAllErrors)
        {
            _showAllValidationFeedback = true;
        }

        var previousFieldKeys = _fieldValidationErrors.Keys.ToHashSet(StringComparer.Ordinal);
        _fieldValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(ConnectionConfig?.Name))
        {
            AddFieldValidationError(NameFieldKey, "连接名称不能为空");
        }
        else
        {
            var duplicateConnection = _savedConnections.FirstOrDefault(c =>
                c.Name.Equals(ConnectionConfig.Name, StringComparison.OrdinalIgnoreCase) &&
                c != SelectedSavedConnection);

            if (duplicateConnection != null)
            {
                AddFieldValidationError(NameFieldKey, $"连接名称 '{ConnectionConfig.Name}' 已存在，请使用其他名称");
            }
        }

        if (IsSQLiteSelected)
        {
            if (string.IsNullOrWhiteSpace(ConnectionConfig?.Database))
            {
                AddFieldValidationError(DatabaseFieldKey, "数据库文件路径不能为空");
            }
            else if (!File.Exists(ConnectionConfig.Database))
            {
                AddFieldValidationError(DatabaseFieldKey, $"Sqlite 数据库文件不存在：{ConnectionConfig.Database}");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(ConnectionConfig?.Host))
            {
                AddFieldValidationError(HostFieldKey, "主机地址不能为空");
            }

            if (ConnectionConfig?.Port <= 0)
            {
                AddFieldValidationError(PortFieldKey, "端口必须大于 0");
            }

            if (ConnectionConfig is { UseWindowsAuthentication: false })
            {
                if (string.IsNullOrWhiteSpace(ConnectionConfig.Username))
                {
                    AddFieldValidationError(UsernameFieldKey, "用户名不能为空");
                }

                if (string.IsNullOrWhiteSpace(ConnectionConfig.Password))
                {
                    AddFieldValidationError(PasswordFieldKey, "密码不能为空");
                }
            }

            if (string.IsNullOrWhiteSpace(ConnectionConfig?.Database))
            {
                AddFieldValidationError(DatabaseFieldKey, "数据库名称不能为空");
            }
        }

        ValidationErrors.Clear();
        foreach (var e in _fieldValidationErrors.Values) ValidationErrors.Add(e);
        OnPropertyChanged(nameof(HasValidationErrors));
        NotifyFieldValidationChanges(previousFieldKeys);
        SaveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
        RefreshDatabasesCommand.NotifyCanExecuteChanged();

        if (showToast && ValidationErrors.Count > 0)
        {
            ShowValidationSummaryToast();
        }

        return ValidationErrors.Count == 0;
    }

    private bool ValidateFormLegacy()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(ConnectionConfig?.Name))
        {
            ValidationErrors.Add("连接名称不能为空");
        }

        if (IsSQLiteSelected)
        {
            if (string.IsNullOrWhiteSpace(ConnectionConfig?.Database))
            {
                ValidationErrors.Add("数据库文件路径不能为空");
            }
            else if (!File.Exists(ConnectionConfig.Database))
            {
                AddFieldValidationError(DatabaseFieldKey, $"Sqlite 数据库文件不存在：{ConnectionConfig.Database}");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(ConnectionConfig?.Host))
            {
                ValidationErrors.Add("主机地址不能为空");
            }

            if (ConnectionConfig?.Port <= 0)
            {
                ValidationErrors.Add("端口必须大于 0");
            }

            // Windows 身份验证时不需要用户名和密码
            if (ConnectionConfig is { UseWindowsAuthentication: false })
            {
                if (string.IsNullOrWhiteSpace(ConnectionConfig.Username))
                {
                    ValidationErrors.Add("用户名不能为空");
                }

                if (string.IsNullOrWhiteSpace(ConnectionConfig.Password))
                {
                    ValidationErrors.Add("密码不能为空");
                }
            }

            if (string.IsNullOrWhiteSpace(ConnectionConfig?.Database))
            {
                ValidationErrors.Add("数据库名称不能为空");
            }
        }

        OnPropertyChanged(nameof(HasValidationErrors));
        SaveCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
        RefreshDatabasesCommand.NotifyCanExecuteChanged();

        return ValidationErrors.Count == 0;
    }

    private bool ValidateConnectionNameUnique()
    {
        if (ConnectionConfig == null || string.IsNullOrWhiteSpace(ConnectionConfig.Name))
        {
            return false;
        }

        ValidateForm(showAllErrors: true);
        return !_fieldValidationErrors.ContainsKey(NameFieldKey);
    }

    private void AddFieldValidationError(string fieldKey, string message)
    {
        _fieldValidationErrors[fieldKey] = message;
    }

    private bool HasFieldValidationError(string fieldKey)
    {
        return ShouldShowFieldValidation(fieldKey) && _fieldValidationErrors.ContainsKey(fieldKey);
    }

    private string GetFieldValidationMessage(string fieldKey)
    {
        return HasFieldValidationError(fieldKey) && _fieldValidationErrors.TryGetValue(fieldKey, out var message)
            ? message
            : string.Empty;
    }

    private bool ShouldShowFieldValidation(string fieldKey)
    {
        return _showAllValidationFeedback || _touchedFields.Contains(fieldKey);
    }

    private void NotifyFieldValidationChanges(HashSet<string> previousFieldKeys)
    {
        foreach (var fieldKey in previousFieldKeys.Union(_fieldValidationErrors.Keys))
        {
            NotifyFieldValidationStateChanged(fieldKey);
        }
    }

    private void NotifyAllFieldValidationStateChanged()
    {
        NotifyFieldValidationStateChanged(NameFieldKey);
        NotifyFieldValidationStateChanged(HostFieldKey);
        NotifyFieldValidationStateChanged(PortFieldKey);
        NotifyFieldValidationStateChanged(UsernameFieldKey);
        NotifyFieldValidationStateChanged(PasswordFieldKey);
        NotifyFieldValidationStateChanged(DatabaseFieldKey);
    }

    private void NotifyFieldValidationStateChanged(string fieldKey)
    {
        switch (fieldKey)
        {
            case NameFieldKey:
                OnPropertyChanged(nameof(IsNameInvalid));
                OnPropertyChanged(nameof(NameValidationMessage));
                break;
            case HostFieldKey:
                OnPropertyChanged(nameof(IsHostInvalid));
                OnPropertyChanged(nameof(HostValidationMessage));
                break;
            case PortFieldKey:
                OnPropertyChanged(nameof(IsPortInvalid));
                OnPropertyChanged(nameof(PortValidationMessage));
                break;
            case UsernameFieldKey:
                OnPropertyChanged(nameof(IsUsernameInvalid));
                OnPropertyChanged(nameof(UsernameValidationMessage));
                break;
            case PasswordFieldKey:
                OnPropertyChanged(nameof(IsPasswordInvalid));
                OnPropertyChanged(nameof(PasswordValidationMessage));
                break;
            case DatabaseFieldKey:
                OnPropertyChanged(nameof(IsDatabaseInvalid));
                OnPropertyChanged(nameof(DatabaseValidationMessage));
                break;
        }
    }

    private void ShowValidationSummaryToast()
    {
        var message = "请先修正以下问题："
            + Environment.NewLine
            + string.Join(Environment.NewLine, ValidationErrors.Select((error, index) => $"{index + 1}. {error}"));

        ToastService.ShowWarning(message, autoCloseDelay: 5000);
    }

    private static string? GetFieldKey(string? propertyName)
    {
        return propertyName switch
        {
            nameof(ConnectionConfig.Name) => NameFieldKey,
            nameof(ConnectionConfig.Host) => HostFieldKey,
            nameof(ConnectionConfig.Port) => PortFieldKey,
            nameof(ConnectionConfig.Username) => UsernameFieldKey,
            nameof(ConnectionConfig.Password) => PasswordFieldKey,
            nameof(ConnectionConfig.Database) => DatabaseFieldKey,
            _ => null
        };
    }

    private bool ValidateConnectionNameUniqueLegacy()
    {
        if (ConnectionConfig == null || string.IsNullOrWhiteSpace(ConnectionConfig.Name))
        {
            return false;
        }

        // 检查是否存在同名连接（排除当前正在编辑的连接）
        var duplicateConnection = _savedConnections.FirstOrDefault(c =>
            c.Name.Equals(ConnectionConfig.Name, StringComparison.OrdinalIgnoreCase) &&
            c != SelectedSavedConnection);

        if (duplicateConnection != null)
        {
            ValidationErrors.Add($"连接名称 '{ConnectionConfig.Name}' 已存在，请使用其他名称");
            OnPropertyChanged(nameof(HasValidationErrors));
            return false;
        }

        return true;
    }

    private void PersistConnections()
    {
        _persistConnections?.Invoke();
    }

    private static ConnectionConfig CreateDefaultConnectionConfig()
    {
        return new ConnectionConfig
        {
            DatabaseType = DatabaseType.PostgresSql,
            Port = 5432
        };
    }

    private bool IsDatabaseTypeEnabled(DatabaseType dbType)
    {
        return DatabaseTypeCards.FirstOrDefault(card => card.Type == dbType)?.IsEnabled ?? true;
    }

    private static ConnectionConfig CloneConnection(ConnectionConfig source)
    {
        return new ConnectionConfig
        {
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            Database = source.Database,
            UseWindowsAuthentication = source.UseWindowsAuthentication,
            GroupId = source.GroupId,
            GroupName = source.GroupName,
            Color = source.Color,
            LastUsedTime = source.LastUsedTime,
            UseCount = source.UseCount
        };
    }

    private static void CopyConnection(ConnectionConfig source, ConnectionConfig target)
    {
        target.Name = source.Name;
        target.DatabaseType = source.DatabaseType;
        target.Host = source.Host;
        target.Port = source.Port;
        target.Username = source.Username;
        target.Password = source.Password;
        target.Database = source.Database;
        target.UseWindowsAuthentication = source.UseWindowsAuthentication;
        target.GroupId = source.GroupId;
        target.GroupName = source.GroupName;
        target.Color = source.Color;
        target.LastUsedTime = source.LastUsedTime;
        target.UseCount = source.UseCount;
    }

    private static string GetDatabaseTypeDisplayName(DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.SqlServer => "SQL Server",
            DatabaseType.MySql => "MySql",
            DatabaseType.PostgresSql => "PostgresSql",
            DatabaseType.Oracle => "Oracle",
            DatabaseType.Sqlite => "Sqlite",
            DatabaseType.Dm => "达梦",
            _ => dbType.ToString()
        };
    }

    public event EventHandler<object?>? RequestClose;

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    protected virtual void OnCloseRequested(bool dialogResult)
    {
        RequestClose?.Invoke(this, dialogResult ? DialogResultConnection : null);
    }
}
