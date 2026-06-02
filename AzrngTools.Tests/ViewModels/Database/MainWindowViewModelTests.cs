using AzrngTools.ViewModels.Database;

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
}
