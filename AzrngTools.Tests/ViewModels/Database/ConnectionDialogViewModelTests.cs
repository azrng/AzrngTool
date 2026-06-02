using Azrng.Core.Model;
using System.Collections.ObjectModel;
using AzrngTools.Models.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class ConnectionDialogViewModelTests
{
    [Fact]
    public void Test_connection_validation_does_not_require_default_database()
    {
        var viewModel = new ConnectionDialogViewModel();
        viewModel.ConnectionConfig = new ConnectionConfig
        {
            Name = "test",
            DatabaseType = DatabaseType.PostgresSql,
            Host = "127.0.0.1",
            Port = 5432,
            Username = "zhangyunpeng",
            Password = "secret",
            Database = string.Empty
        };

        var canTestConnection = viewModel.ValidateForm(
            showAllErrors: true,
            requireDatabase: false);

        Assert.True(canTestConnection);
        Assert.DoesNotContain("数据库名称不能为空", viewModel.ValidationErrors);
    }

    [Fact]
    public void Save_validation_requires_default_database_for_server_connections()
    {
        var viewModel = new ConnectionDialogViewModel();
        viewModel.ConnectionConfig = new ConnectionConfig
        {
            Name = "test",
            DatabaseType = DatabaseType.PostgresSql,
            Host = "127.0.0.1",
            Port = 5432,
            Username = "zhangyunpeng",
            Password = "secret",
            Database = string.Empty
        };

        var canSave = viewModel.ValidateForm(showAllErrors: true);

        Assert.False(canSave);
        Assert.Contains("数据库名称不能为空", viewModel.ValidationErrors);
    }

    [Fact]
    public void Editing_existing_connection_from_equivalent_instance_does_not_report_duplicate_name()
    {
        var savedConnection = CreatePostgreSqlConnection("172.16.0.20", "old-password");
        var selectedConnection = CreatePostgreSqlConnection("172.16.0.20", "old-password");
        var savedConnections = new ObservableCollection<ConnectionConfig> { savedConnection };
        var viewModel = new ConnectionDialogViewModel(savedConnections, selectedConnection: selectedConnection);

        viewModel.ConnectionConfig.Password = "new-password";

        var canSave = viewModel.ValidateForm(showAllErrors: true);

        Assert.True(canSave);
        Assert.DoesNotContain(
            viewModel.ValidationErrors,
            error => error.Contains("连接名称", StringComparison.Ordinal) &&
                     error.Contains("已存在", StringComparison.Ordinal));
    }

    [Fact]
    public void Test_connection_validation_does_not_require_unique_connection_name()
    {
        var savedConnections = new ObservableCollection<ConnectionConfig>
        {
            CreatePostgreSqlConnection("172.16.0.20", "old-password")
        };
        var viewModel = new ConnectionDialogViewModel(savedConnections)
        {
            ConnectionConfig = CreatePostgreSqlConnection("172.16.0.20", "new-password")
        };

        var canTestConnection = viewModel.ValidateForm(
            showAllErrors: true,
            requireDatabase: false,
            requireUniqueName: false);

        Assert.True(canTestConnection);
        Assert.DoesNotContain(
            viewModel.ValidationErrors,
            error => error.Contains("连接名称", StringComparison.Ordinal) &&
                     error.Contains("已存在", StringComparison.Ordinal));
    }

    private static ConnectionConfig CreatePostgreSqlConnection(string name, string password)
    {
        return new ConnectionConfig
        {
            Name = name,
            DatabaseType = DatabaseType.PostgresSql,
            Host = "172.16.0.20",
            Port = 5432,
            Username = "zhangyunpeng",
            Password = password,
            Database = "mdm_dev"
        };
    }
}
