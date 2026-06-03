using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Services.Database;

public partial class ConnectionManagementCoordinator : ObservableObject, IConnectionManagementCoordinator
{
    private const string ConfigFileName = "connections.json";
    private const string GroupsFileName = "groups.json";

    private readonly string _configFilePath;
    private readonly string _groupsFilePath;
    private readonly IConnectionConfigurationService _connectionConfigurationService;
    private readonly IConnectionGroupConfigurationService _connectionGroupConfigurationService;
    private readonly IDatabaseConnectionContextService _databaseConnectionContextService;

    [ObservableProperty]
    private ObservableCollection<ConnectionConfig> _connections = new();

    [ObservableProperty]
    private ObservableCollection<ConnectionGroup> _groups = new();

    [ObservableProperty]
    private string? _selectedGroupId;

    [ObservableProperty]
    private string _sortMode = "Name";

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ConnectionManagementCoordinator()
        : this(null, null, null)
    {
    }

    public ConnectionManagementCoordinator(
        IConnectionConfigurationService? connectionConfigurationService = null,
        IConnectionGroupConfigurationService? connectionGroupConfigurationService = null,
        IDatabaseConnectionContextService? databaseConnectionContextService = null)
    {
        _connectionConfigurationService = connectionConfigurationService ?? new ConnectionConfigurationService();
        _connectionGroupConfigurationService = connectionGroupConfigurationService ?? new ConnectionGroupConfigurationService();
        _databaseConnectionContextService = databaseConnectionContextService ?? new DatabaseConnectionContextService();

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartDbSql");
        Directory.CreateDirectory(appDataDir);
        _configFilePath = Path.Combine(appDataDir, ConfigFileName);
        _groupsFilePath = Path.Combine(appDataDir, GroupsFileName);

        LoadGroups();
        LoadConnections();
    }

    public void LoadGroups()
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

    public void SaveGroups()
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

    public void LoadConnections()
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

    public void SaveConnections()
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

    public void AddConnection(ConnectionConfig connection)
    {
        if (connection == null) return;
        Connections.Add(connection);
        SaveConnections();
    }

    public void DeleteConnection(ConnectionConfig connection)
    {
        if (connection == null) return;
        Connections.Remove(connection);
        SaveConnections();
    }

    public async Task ExportConnectionsAsync(Window? ownerWindow)
    {
        try
        {
            if (ownerWindow == null)
            {
                LoggingService.LogWarning("MainWindow is not available.");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(ownerWindow);
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

            if (file == null) return;

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

    public async Task ImportConnectionsAsync(Window? ownerWindow)
    {
        try
        {
            if (ownerWindow == null)
            {
                LoggingService.LogWarning("MainWindow is not available.");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(ownerWindow);
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

            if (files.Count == 0) return;

            var json = await File.ReadAllTextAsync(files[0].Path.LocalPath);
            var importedConnections = _connectionConfigurationService.DeserializeConnections(json);
            if (importedConnections == null || importedConnections.Count == 0)
            {
                ToastService.ShowWarning("所选文件中没有有效的连接配置。", 4000);
                return;
            }

            var importResult = _connectionConfigurationService.BuildImportResult(Connections, importedConnections);
            foreach (var connection in importResult.ImportedConnections)
            {
                Connections.Add(connection);
            }

            SaveConnections();

            LoggingService.LogOperation(importResult.Message);
            ToastService.ShowSuccess(importResult.Message, 4000);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("导入连接失败。", ex);
            ToastService.ShowError($"导入失败：{ex.Message}", 6000);
        }
    }

    public void AddGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        Groups.Add(_connectionGroupConfigurationService.CreateGroup(groupName));
        SaveGroups();
        LoggingService.LogInfo($"Added group: {groupName}");
    }

    public void DeleteGroup(string groupId)
    {
        var groupName = _connectionGroupConfigurationService.RemoveGroupAndClearConnections(Groups, Connections, groupId);
        if (groupName == null) return;
        SaveGroups();
        SaveConnections();
        LoggingService.LogInfo($"Deleted group: {groupName}");
    }

    public void ChangeGroupFilter(string? groupId)
    {
        SelectedGroupId = groupId;
        LoggingService.LogInfo($"Changed group filter to {groupId ?? "All"}.");
    }

    public void ChangeSort(string sortMode)
    {
        SortMode = sortMode;
        var sorted = _databaseConnectionContextService.SortConnections(Connections, SortMode);
        Connections.Clear();
        foreach (var c in sorted) Connections.Add(c);
        LoggingService.LogInfo($"Changed connection sort mode to {sortMode}.");
    }

    public ConnectionConfig ResolveConnection(ConnectionConfig connection)
    {
        return _databaseConnectionContextService.ResolveConnectionFromCollection(Connections, connection);
    }
}
