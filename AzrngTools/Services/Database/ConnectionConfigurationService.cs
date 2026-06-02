using System.Text.Json;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public class ConnectionConfigurationService : IConnectionConfigurationService, ISingletonDependency
{
    private static readonly JsonSerializerOptions ConnectionJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public IReadOnlyList<ConnectionConfig> LoadConnections(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var json = File.ReadAllText(filePath);
        return DeserializeConnections(json);
    }

    public void SaveConnections(string filePath, IEnumerable<ConnectionConfig> connections)
    {
        var json = SerializeConnections(connections);
        File.WriteAllText(filePath, json);
    }

    public IReadOnlyList<ConnectionConfig> DeserializeConnections(string json)
    {
        var connections = JsonSerializer.Deserialize<List<ConnectionConfig>>(json, ConnectionJsonOptions)
            ?? [];

        foreach (var connection in connections)
        {
            DecryptConnectionPassword(connection);
        }

        return connections;
    }

    public string SerializeConnections(IEnumerable<ConnectionConfig> connections)
    {
        return JsonSerializer.Serialize(CreateEncryptedConnectionCopies(connections), ConnectionJsonOptions);
    }

    public List<ConnectionConfig> CreateEncryptedConnectionCopies(IEnumerable<ConnectionConfig> connections)
    {
        return connections.Select(CreateEncryptedConnectionCopy).ToList();
    }

    public ConnectionImportResult BuildImportResult(
        IEnumerable<ConnectionConfig> existingConnections,
        IEnumerable<ConnectionConfig> importedConnections)
    {
        var existingNames = existingConnections
            .Select(connection => connection.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var importableConnections = new List<ConnectionConfig>();
        var skippedCount = 0;

        foreach (var connection in importedConnections)
        {
            if (existingNames.Contains(connection.Name))
            {
                skippedCount++;
                continue;
            }

            importableConnections.Add(connection);
            existingNames.Add(connection.Name);
        }

        return new ConnectionImportResult(importableConnections, skippedCount);
    }

    private static void DecryptConnectionPassword(ConnectionConfig connection)
    {
        if (!string.IsNullOrWhiteSpace(connection.Password))
        {
            connection.SetDecryptedPassword(connection.Password);
        }
    }

    private static ConnectionConfig CreateEncryptedConnectionCopy(ConnectionConfig source)
    {
        var copy = new ConnectionConfig
        {
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            Database = source.Database,
            UseWindowsAuthentication = source.UseWindowsAuthentication,
            LastUsedTime = source.LastUsedTime,
            UseCount = source.UseCount,
            GroupId = source.GroupId,
            GroupName = source.GroupName,
            Color = source.Color
        };

        if (!string.IsNullOrWhiteSpace(copy.Password))
        {
            copy.Password = copy.GetEncryptedPassword();
        }

        return copy;
    }
}
