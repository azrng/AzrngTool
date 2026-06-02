using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IDatabaseConnectionContextService
{
    IReadOnlyList<ConnectionConfig> SortConnections(IEnumerable<ConnectionConfig> connections, string sortMode);

    ConnectionConfig ResolveConnectionFromCollection(IEnumerable<ConnectionConfig> connections, ConnectionConfig connection);

    ConnectionConfig CreateRuntimeConnection(ConnectionConfig source, string? databaseName);

    DatabaseSelectionResult BuildDatabaseSelection(IEnumerable<string> databases, string? preferredDatabase, bool loadSucceeded);
}
