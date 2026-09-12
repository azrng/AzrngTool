using System.Text.Json;
using System.Text.Json.Serialization;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public partial class ConnectionGroupConfigurationService : IConnectionGroupConfigurationService, ISingletonDependency
{
    // 源生成 JSON 上下文：AOT 发布下不能用反射序列化；读侧保持大小写不敏感以兼容历史文件
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(List<ConnectionGroup>))]
    private sealed partial class ConnectionGroupJsonContext : JsonSerializerContext
    {
    }

    public IReadOnlyList<ConnectionGroup> LoadGroups(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize(File.ReadAllText(filePath), ConnectionGroupJsonContext.Default.ListConnectionGroup)
            ?? [];
    }

    public void SaveGroups(string filePath, IEnumerable<ConnectionGroup> groups)
    {
        var json = JsonSerializer.Serialize(groups.ToList(), ConnectionGroupJsonContext.Default.ListConnectionGroup);
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
