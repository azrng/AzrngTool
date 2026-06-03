using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IConnectionManagementCoordinator
{
    ObservableCollection<ConnectionConfig> Connections { get; }
    ObservableCollection<ConnectionGroup> Groups { get; }
    string? SelectedGroupId { get; set; }
    string SortMode { get; set; }
    string SearchText { get; set; }

    void LoadConnections();
    void SaveConnections();
    void AddConnection(ConnectionConfig connection);
    void DeleteConnection(ConnectionConfig connection);
    Task ExportConnectionsAsync(Window? ownerWindow);
    Task ImportConnectionsAsync(Window? ownerWindow);
    void LoadGroups();
    void SaveGroups();
    void AddGroup(string groupName);
    void DeleteGroup(string groupId);
    void ChangeGroupFilter(string? groupId);
    void ChangeSort(string sortMode);
    ConnectionConfig ResolveConnection(ConnectionConfig connection);
}
