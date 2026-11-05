using System.Text.Json;
using AzrngTools.Services;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Format;
using Xunit;

namespace AzrngTools.Tests.ViewModels.Format;

// 示例页输入记忆回归：页面构造恢复、连续变更合并、敏感页键排除模式由 Encrypts 批次覆盖
public class JsonPageViewModelInputRestoreTests
{
    private const string PageId = "format-json";

    [Fact]
    public void SetOriginalThenNewInstance_RestoresInputText()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new PageInputStoreService(directory);
            var first = new JsonPageViewModel(new TestMessageService(), store);
            first.Original = "{ \"a\": 1 }";
            first.Original = "{ \"a\": 2 }";
            store.FlushPendingPagesForTests();

            // 页面实例经 _pageCache 缓存复用，这里用新实例模拟应用重启后首次进入页面
            var second = new JsonPageViewModel(new TestMessageService(), store);

            Assert.Equal("{ \"a\": 2 }", second.Original);
            Assert.False(File.Exists(Path.Combine(directory, PageId + ".json.tmp")), "原子替换后不应残留临时文件。");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RestoreAssignment_DoesNotSubmitNewSnapshot()
    {
        var directory = CreateTempDirectory();
        try
        {
            // 通过服务本身预置存档，规避测试手写 JSON 与源生成命名策略漂移
            var store = new PageInputStoreService(directory);
            store.Save(PageId, new Dictionary<string, string?> { ["Original"] = "已存档内容" });
            store.FlushPendingPagesForTests();

            var viewModel = new JsonPageViewModel(new TestMessageService(), store);
            Assert.Equal("已存档内容", viewModel.Original);

            // 恢复赋值被 _isRestoring 拦截，不应产生新的待写页（否则恢复即回写）
            Assert.Equal(0, store.PendingPageCountForTests);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "json-page-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
