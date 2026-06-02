using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class ConnectionConfigurationServiceTests
{
    [Fact]
    public void Encrypted_connection_copies_do_not_modify_source_passwords()
    {
        var service = new ConnectionConfigurationService();
        var connections = new List<ConnectionConfig>
        {
            CreateConnection()
        };

        var encryptedCopies = service.CreateEncryptedConnectionCopies(connections);

        Assert.Single(encryptedCopies);
        Assert.Equal("plain-password", connections[0].Password);
        Assert.NotEqual(connections[0].Password, encryptedCopies[0].Password);

        encryptedCopies[0].SetDecryptedPassword(encryptedCopies[0].Password);
        Assert.Equal("plain-password", encryptedCopies[0].Password);
        Assert.Equal(connections[0].GroupName, encryptedCopies[0].GroupName);
        Assert.Equal(connections[0].UseCount, encryptedCopies[0].UseCount);
    }

    [Fact]
    public void Serialize_and_deserialize_roundtrip_keeps_runtime_password_decrypted()
    {
        var service = new ConnectionConfigurationService();
        var source = CreateConnection();

        var json = service.SerializeConnections(new[] { source });
        var imported = service.DeserializeConnections(json);

        var connection = Assert.Single(imported);
        Assert.Equal("plain-password", source.Password);
        Assert.Equal("plain-password", connection.Password);
        Assert.Equal(source.Name, connection.Name);
        Assert.Equal(source.GroupId, connection.GroupId);
        Assert.Equal(source.GroupName, connection.GroupName);
    }

    [Fact]
    public void BuildImportResult_skips_existing_and_batch_duplicate_connection_names()
    {
        var service = new ConnectionConfigurationService();
        var existing = new[]
        {
            CreateConnection("prod")
        };
        var imported = new[]
        {
            CreateConnection("PROD"),
            CreateConnection("stage"),
            CreateConnection("Stage"),
            CreateConnection("test")
        };

        var result = service.BuildImportResult(existing, imported);

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Equal(["stage", "test"], result.ImportedConnections.Select(connection => connection.Name));
        Assert.Equal("已导入 2 个连接，跳过 2 个重复项。", result.Message);
    }

    [Fact]
    public void BuildImportResult_uses_imported_count_message_when_no_duplicates()
    {
        var service = new ConnectionConfigurationService();

        var result = service.BuildImportResult([], [CreateConnection("prod")]);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal("已导入 1 个连接。", result.Message);
    }

    private static ConnectionConfig CreateConnection()
    {
        return CreateConnection("pg");
    }

    private static ConnectionConfig CreateConnection(string name)
    {
        return new ConnectionConfig
        {
            Name = name,
            DatabaseType = DatabaseType.PostgresSql,
            Host = "127.0.0.1",
            Port = 5432,
            Username = "user",
            Password = "plain-password",
            Database = "postgres",
            LastUsedTime = new DateTime(2026, 6, 2, 17, 10, 0),
            UseCount = 3,
            GroupId = "group-1",
            GroupName = "生产",
            Color = "#ff0000"
        };
    }
}
