using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IConnectionConfigurationService
{
    IReadOnlyList<ConnectionConfig> LoadConnections(string filePath);

    void SaveConnections(string filePath, IEnumerable<ConnectionConfig> connections);

    IReadOnlyList<ConnectionConfig> DeserializeConnections(string json);

    string SerializeConnections(IEnumerable<ConnectionConfig> connections);

    List<ConnectionConfig> CreateEncryptedConnectionCopies(IEnumerable<ConnectionConfig> connections);
}
