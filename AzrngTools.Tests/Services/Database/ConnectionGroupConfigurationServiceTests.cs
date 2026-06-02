using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class ConnectionGroupConfigurationServiceTests
{
    [Fact]
    public void CreateDefaultGroup_returns_default_group()
    {
        var service = new ConnectionGroupConfigurationService();

        var group = service.CreateDefaultGroup();

        Assert.Equal("default", group.Id);
        Assert.Equal("Default", group.Name);
        Assert.True(group.IsDefault);
    }

    [Fact]
    public void CreateGroup_trims_name_and_uses_default_color()
    {
        var service = new ConnectionGroupConfigurationService();

        var group = service.CreateGroup("  生产  ");

        Assert.Equal("生产", group.Name);
        Assert.Equal("#E3EFE8", group.Color);
        Assert.False(group.IsDefault);
    }

    [Fact]
    public void RemoveGroupAndClearConnections_removes_group_and_clears_connection_references()
    {
        var service = new ConnectionGroupConfigurationService();
        var groups = new List<ConnectionGroup>
        {
            new() { Id = "prod", Name = "生产" },
            new() { Id = "dev", Name = "开发" }
        };
        var connections = new List<ConnectionConfig>
        {
            CreateConnection("prod-1", "prod", "生产"),
            CreateConnection("dev-1", "dev", "开发"),
            CreateConnection("prod-2", "prod", "生产")
        };

        var removedGroupName = service.RemoveGroupAndClearConnections(groups, connections, "prod");

        Assert.Equal("生产", removedGroupName);
        Assert.DoesNotContain(groups, group => group.Id == "prod");
        Assert.Null(connections[0].GroupId);
        Assert.Null(connections[0].GroupName);
        Assert.Equal("dev", connections[1].GroupId);
        Assert.Equal("开发", connections[1].GroupName);
        Assert.Null(connections[2].GroupId);
        Assert.Null(connections[2].GroupName);
    }

    [Fact]
    public void RemoveGroupAndClearConnections_keeps_default_group()
    {
        var service = new ConnectionGroupConfigurationService();
        var defaultGroup = service.CreateDefaultGroup();
        var groups = new List<ConnectionGroup> { defaultGroup };
        var connections = new List<ConnectionConfig>
        {
            CreateConnection("default-1", defaultGroup.Id, defaultGroup.Name)
        };

        var removedGroupName = service.RemoveGroupAndClearConnections(groups, connections, defaultGroup.Id);

        Assert.Null(removedGroupName);
        Assert.Single(groups);
        Assert.Equal(defaultGroup.Id, connections[0].GroupId);
        Assert.Equal(defaultGroup.Name, connections[0].GroupName);
    }

    [Fact]
    public void Save_and_load_groups_roundtrip()
    {
        var service = new ConnectionGroupConfigurationService();
        var filePath = Path.Combine(Path.GetTempPath(), $"azrng-groups-{Guid.NewGuid():N}.json");
        var groups = new List<ConnectionGroup>
        {
            new()
            {
                Id = "dev",
                Name = "开发",
                Description = "开发环境",
                Color = "#00ff00",
                IsDefault = false
            }
        };

        try
        {
            service.SaveGroups(filePath, groups);

            var loadedGroups = service.LoadGroups(filePath);

            var loadedGroup = Assert.Single(loadedGroups);
            Assert.Equal("dev", loadedGroup.Id);
            Assert.Equal("开发", loadedGroup.Name);
            Assert.Equal("开发环境", loadedGroup.Description);
            Assert.Equal("#00ff00", loadedGroup.Color);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static ConnectionConfig CreateConnection(string name, string groupId, string groupName)
    {
        return new ConnectionConfig
        {
            Name = name,
            DatabaseType = DatabaseType.PostgresSql,
            Host = "127.0.0.1",
            Port = 5432,
            Username = "user",
            Password = "pwd",
            Database = "postgres",
            GroupId = groupId,
            GroupName = groupName
        };
    }
}
