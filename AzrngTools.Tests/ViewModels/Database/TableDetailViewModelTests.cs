using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class TableDetailViewModelTests
{
    [Fact]
    public void RowCountDisplay_shows_not_loaded_when_row_count_is_unknown()
    {
        var viewModel = new TableDetailViewModel
        {
            RowCount = -1
        };

        Assert.Equal("未加载", viewModel.RowCountDisplay);
    }
}
