using System.Text.Json;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public class ConnectionGroupConfigurationService : IConnectionGroupConfigurationService, ISingletonDependency
{
    private static readonly JsonSerializerOptions GroupJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public IReadOnlyList<ConnectionGroup> LoadGroups(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ConnectionGroup>>(File.ReadAllText(filePath), GroupJsonOptions)
            ?? [];
    }

    public void SaveGroups(string filePath, IEnumerable<ConnectionGroup> groups)
    {
        var json = JsonSerializer.Serialize(groups.ToList(), GroupJsonOptions);
        File.WriteAllText(filePath, json);
    }

    public ConnectionGroup CreateDefaultGroup()
    {
        return new ConnectionGroup
        {
            Id = "default",
            Name = "Default",
            Description = "Default connection group",
            Color = "#E3EFE8",
            IsDefault = true
        };
    }

    public ConnectionGroup CreateGroup(string groupName)
    {
        return new ConnectionGroup
        {
            Name = groupName.Trim(),
            Color = "#E3EFE8"
        };
    }

    public string? RemoveGroupAndClearConnections(
        ICollection<ConnectionGroup> groups,
        IEnumerable<ConnectionConfig> connections,
        string groupId)
    {
        var group = groups.FirstOrDefault(item => item.Id == groupId);
        if (group == null || group.IsDefault)
        {
            return null;
        }

        var groupName = group.Name;
        groups.Remove(group);

        foreach (var connection in connections.Where(connection => connection.GroupId == groupId))
        {
            connection.GroupId = null;
            connection.GroupName = null;
        }

        return groupName;
    }
}
