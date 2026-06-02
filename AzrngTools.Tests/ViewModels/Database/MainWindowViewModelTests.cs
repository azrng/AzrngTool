using System.Reflection;
using AzrngTools.Models.Database;
using AzrngTools.ViewModels.Database;
using Azrng.Core.Model;

namespace AzrngTools.Tests.ViewModels.Database;

public class MainWindowViewModelTests
{
    [Fact]
    public void Database_search_filters_available_databases_without_changing_selection()
    {
        var viewModel = new MainWindowViewModel
        {
            SelectedDatabaseName = "cdr_stage1"
        };
        viewModel.AvailableDatabases.Add("cdr_stage1");
        viewModel.AvailableDatabases.Add("cdr_test_100");
        viewModel.AvailableDatabases.Add("cdss_2_1_0");
        viewModel.AvailableDatabases.Add("cdss_bdy");

        viewModel.DatabaseSearchText = "test";

        Assert.Equal(["cdr_test_100"], viewModel.FilteredAvailableDatabases);
        Assert.Equal("cdr_stage1", viewModel.SelectedDatabaseName);
    }

    [Fact]
    public void Encrypted_connection_copies_do_not_modify_source_passwords()
    {
        var connections = new List<ConnectionConfig>
        {
            new()
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
            }
        };
        var method = typeof(MainWindowViewModel).GetMethod(
            "CreateEncryptedConnectionCopies",
            BindingFlags.Static | BindingFlags.NonPublic);

        var encryptedCopies = Assert.IsType<List<ConnectionConfig>>(method?.Invoke(null, [connections]));

        Assert.Single(encryptedCopies);
        Assert.Equal("plain-password", connections[0].Password);
        Assert.NotEqual(connections[0].Password, encryptedCopies[0].Password);

        encryptedCopies[0].SetDecryptedPassword(encryptedCopies[0].Password);
        Assert.Equal("plain-password", encryptedCopies[0].Password);
        Assert.Equal(connections[0].GroupName, encryptedCopies[0].GroupName);
        Assert.Equal(connections[0].UseCount, encryptedCopies[0].UseCount);
    }
}
