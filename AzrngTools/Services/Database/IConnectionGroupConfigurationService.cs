using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IConnectionGroupConfigurationService
{
    IReadOnlyList<ConnectionGroup> LoadGroups(string filePath);

    void SaveGroups(string filePath, IEnumerable<ConnectionGroup> groups);

    ConnectionGroup CreateDefaultGroup();

    ConnectionGroup CreateGroup(string groupName);

    string? RemoveGroupAndClearConnections(
        ICollection<ConnectionGroup> groups,
        IEnumerable<ConnectionConfig> connections,
        string groupId);
}
