using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public class DatabaseConnectionContextService : IDatabaseConnectionContextService, ISingletonDependency
{
    public IReadOnlyList<ConnectionConfig> SortConnections(IEnumerable<ConnectionConfig> connections, string sortMode)
    {
        return sortMode switch
        {
            "Name" => connections.OrderBy(connection => connection.Name).ToList(),
            "Type" => connections.OrderBy(connection => connection.DatabaseType).ToList(),
            "LastUsed" => connections.OrderByDescending(connection => connection.LastUsedTime ?? DateTime.MinValue).ToList(),
            "UsageCount" => connections.OrderByDescending(connection => connection.UseCount).ToList(),
            _ => connections.OrderBy(connection => connection.Name).ToList()
        };
    }

    public ConnectionConfig ResolveConnectionFromCollection(IEnumerable<ConnectionConfig> connections, ConnectionConfig connection)
    {
        return connections.FirstOrDefault(item => ReferenceEquals(item, connection))
               ?? connections.FirstOrDefault(item =>
                   string.Equals(item.Name, connection.Name, StringComparison.OrdinalIgnoreCase))
               ?? connection;
    }

    public ConnectionConfig CreateRuntimeConnection(ConnectionConfig source, string? databaseName)
    {
        return new ConnectionConfig
        {
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            Database = string.IsNullOrWhiteSpace(databaseName) ? source.Database : databaseName,
            UseWindowsAuthentication = source.UseWindowsAuthentication,
            GroupId = source.GroupId,
            GroupName = source.GroupName,
            Color = source.Color,
            LastUsedTime = source.LastUsedTime,
            UseCount = source.UseCount
        };
    }

    public DatabaseSelectionResult BuildDatabaseSelection(IEnumerable<string> databases, string? preferredDatabase, bool loadSucceeded)
    {
        var mergedDatabases = databases
            .Where(database => !string.IsNullOrWhiteSpace(database))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredDatabase) &&
            !mergedDatabases.Contains(preferredDatabase, StringComparer.OrdinalIgnoreCase))
        {
            mergedDatabases.Insert(0, preferredDatabase);
        }

        if (!loadSucceeded && !string.IsNullOrWhiteSpace(preferredDatabase) && mergedDatabases.Count == 0)
        {
            mergedDatabases.Add(preferredDatabase);
        }

        var selectedDatabase = string.IsNullOrWhiteSpace(preferredDatabase)
            ? mergedDatabases.FirstOrDefault()
            : preferredDatabase;

        return new DatabaseSelectionResult(mergedDatabases, selectedDatabase);
    }
}
