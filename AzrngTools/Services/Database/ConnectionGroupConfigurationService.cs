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
}
