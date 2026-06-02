using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseConnectionContextServiceTests
{
    [Fact]
    public void SortConnections_orders_by_usage_count_descending()
    {
        var service = new DatabaseConnectionContextService();
        var low = CreateConnection("low", useCount: 1);
        var high = CreateConnection("high", useCount: 5);

        var result = service.SortConnections([low, high], "UsageCount");

        Assert.Equal([high, low], result);
    }

    [Fact]
    public void ResolveConnectionFromCollection_prefers_existing_reference_by_name()
    {
        var service = new DatabaseConnectionContextService();
        var existing = CreateConnection("Prod");
        var returned = CreateConnection("prod");

        var result = service.ResolveConnectionFromCollection([existing], returned);

        Assert.Same(existing, result);
    }

    [Fact]
    public void CreateRuntimeConnection_uses_selected_database_without_mutating_source()
    {
        var service = new DatabaseConnectionContextService();
        var source = CreateConnection("prod", database: "default_db");

        var result = service.CreateRuntimeConnection(source, "selected_db");

        Assert.NotSame(source, result);
        Assert.Equal("selected_db", result.Database);
        Assert.Equal("default_db", source.Database);
        Assert.Equal(source.Host, result.Host);
        Assert.Equal(source.Password, result.Password);
        Assert.Equal(source.GroupId, result.GroupId);
    }

    [Fact]
    public void BuildDatabaseSelection_merges_preferred_database_and_removes_duplicates()
    {
        var service = new DatabaseConnectionContextService();

        var result = service.BuildDatabaseSelection(["main", "MAIN", "", "logs"], "archive", loadSucceeded: true);

        Assert.Equal(["archive", "main", "logs"], result.Databases);
        Assert.Equal("archive", result.SelectedDatabase);
    }

    [Fact]
    public void BuildDatabaseSelection_selects_first_loaded_database_when_preferred_is_empty()
    {
        var service = new DatabaseConnectionContextService();

        var result = service.BuildDatabaseSelection(["main", "logs"], null, loadSucceeded: true);

        Assert.Equal(["main", "logs"], result.Databases);
        Assert.Equal("main", result.SelectedDatabase);
    }

    [Fact]
    public void BuildDatabaseSelection_keeps_preferred_database_when_load_fails_without_databases()
    {
        var service = new DatabaseConnectionContextService();

        var result = service.BuildDatabaseSelection([], "fallback", loadSucceeded: false);

        Assert.Equal(["fallback"], result.Databases);
        Assert.Equal("fallback", result.SelectedDatabase);
    }

    private static ConnectionConfig CreateConnection(
        string name,
        string database = "app",
        int useCount = 0)
    {
        return new ConnectionConfig
        {
            Name = name,
            DatabaseType = DatabaseType.PostgresSql,
            Host = "localhost",
            Port = 5432,
            Username = "postgres",
            Password = "secret",
            Database = database,
            GroupId = "default",
            GroupName = "default",
            Color = "#336699",
            LastUsedTime = DateTime.Today,
            UseCount = useCount
        };
    }
}
