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
}
