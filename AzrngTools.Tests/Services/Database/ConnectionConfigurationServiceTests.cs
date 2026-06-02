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

    private static ConnectionConfig CreateConnection()
    {
        return new ConnectionConfig
        {
            Name = "pg",
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
