using AzrngTools.ViewModels.Database;
using AzrngTools.Models.Database;

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
    public void Database_search_replaces_filtered_database_collection()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AvailableDatabases.Add("cdr_stage1");
        viewModel.AvailableDatabases.Add("cdr_test_100");
        var originalCollection = viewModel.FilteredAvailableDatabases;
        var propertyChangedNames = new List<string?>();
        viewModel.PropertyChanged += (_, args) => propertyChangedNames.Add(args.PropertyName);

        viewModel.DatabaseSearchText = "test";

        Assert.NotSame(originalCollection, viewModel.FilteredAvailableDatabases);
        Assert.Equal(["cdr_test_100"], viewModel.FilteredAvailableDatabases);
        Assert.Contains(nameof(MainWindowViewModel.FilteredAvailableDatabases), propertyChangedNames);
    }

    [Fact]
    public void Activating_table_folder_closes_previous_table_detail()
    {
        var viewModel = new MainWindowViewModel
        {
            ShowOverviewPage = false,
            CurrentWorkspaceMode = DetailWorkspaceMode.Table
        };
        viewModel.TableDetailViewModel.Tables.Add(new TableModel { Name = "ai_mapdata", Schema = "mdm" });
        viewModel.TableDetailViewModel.SelectedTable = viewModel.TableDetailViewModel.Tables[0];
        viewModel.TableDetailViewModel.ShowObjectList = false;

        viewModel.ActivateWorkspaceFolder("Tables");

        Assert.True(viewModel.ShowTableWorkspace);
        Assert.True(viewModel.TableDetailViewModel.ShowObjectList);
        Assert.Null(viewModel.TableDetailViewModel.SelectedTable);
    }

}
