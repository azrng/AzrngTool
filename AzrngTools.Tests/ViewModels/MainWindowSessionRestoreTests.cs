using Avalonia.Styling;
using AzrngTools.Services;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzrngTools.Tests.ViewModels;

// 主窗口会话恢复回归：上次页面导航与侧栏折叠按存档还原，无存档时保持默认首页
public class MainWindowSessionRestoreTests
{
    [Fact]
    public void RestoreLastSession_WithArchive_NavigatesToLastPageAndRestoresSidebar()
    {
        var store = TestPageInputStores.Create();
        store.Save("main-window", new Dictionary<string, string?>
        {
            ["IsSidebarCollapsed"] = "true",
            ["SelectedListItem"] = "AzrngTools.ViewModels.Format.SqlFormatPageViewModel",
        });
        store.FlushPendingPagesForTests();

        var viewModel = CreateViewModel(store);
        viewModel.RestoreLastSession();

        Assert.True(viewModel.IsSidebarCollapsed);
        Assert.Equal(
            "AzrngTools.ViewModels.Format.SqlFormatPageViewModel",
            viewModel.SelectedListItem?.MenuType?.FullName);
    }

    [Fact]
    public void RestoreLastSession_WithoutArchive_StaysOnHomeAndEnablesSaving()
    {
        var store = TestPageInputStores.Create();

        var viewModel = CreateViewModel(store);
        viewModel.RestoreLastSession();

        Assert.False(viewModel.IsSidebarCollapsed);
        Assert.Equal(typeof(OverviewPageViewModel), viewModel.SelectedListItem?.MenuType);

        // 恢复完成后放行存档提交：切换页面应产生新的待写快照
        viewModel.ToggleSidebarCommand.Execute(null);
        store.FlushPendingPagesForTests();

        Assert.Equal("true", store.TryLoad("main-window")["IsSidebarCollapsed"]);
    }

    [Fact]
    public void RestoreLastSession_WithUnknownMenuKey_StaysOnHome()
    {
        var store = TestPageInputStores.Create();
        store.Save("main-window", new Dictionary<string, string?>
        {
            ["SelectedListItem"] = "AzrngTools.ViewModels.Gone.RemovedPageViewModel",
        });
        store.FlushPendingPagesForTests();

        var viewModel = CreateViewModel(store);
        viewModel.RestoreLastSession();

        // 存档里的页面已不存在（改名/移除），保持首页不导航
        Assert.Equal(typeof(OverviewPageViewModel), viewModel.SelectedListItem?.MenuType);
    }

    private static MainWindowViewModel CreateViewModel(IPageInputStoreService store)
    {
        // 页面解析用空容器：恢复断言只关心选中项与侧栏状态，不实际创建页面实例
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new MainWindowViewModel(
            serviceProvider,
            new TestToolUsageStatsService(),
            new TestThemePreferenceService(),
            store);
    }

    private sealed class TestToolUsageStatsService : IToolUsageStatsService
    {
        public void RecordToolUsage(string menuKey, string title)
        {
        }

        public IReadOnlyList<string> GetTopToolKeys(int maxCount)
        {
            return [];
        }
    }

    private sealed class TestThemePreferenceService : IThemePreferenceService
    {
        public ThemeVariant LoadRequestedThemeVariant()
        {
            return ThemeVariant.Default;
        }

        public void SaveRequestedThemeVariant(ThemeVariant themeVariant)
        {
        }
    }
}
