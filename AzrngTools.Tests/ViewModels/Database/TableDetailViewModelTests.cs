using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class TableDetailViewModelTests
{
    private static IDatabaseService CreateDatabaseService() => new DatabaseService();

    [Fact]
    public void RowCountDisplay_shows_not_loaded_when_row_count_is_unknown()
    {
        var viewModel = new TableDetailViewModel(CreateDatabaseService())
        {
            RowCount = -1
        };

        Assert.Equal("未加载", viewModel.RowCountDisplay);
    }
}
