using AzrngTools.Services;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Other;
using Xunit;

namespace AzrngTools.Tests.ViewModels.Other;

// Unix时间戳页输入记忆回归：文本键原样往返、数值键（TimeSpan ticks）经 DecodeInt64 往返、
// DateTime 键经 DecodeDateTime 往返，以及恢复赋值不产生新快照
public class UnixTimestampInputRestoreTests
{
    private const string PageId = "other-unix-timestamp";

    [Fact]
    public void SetInputsThenNewInstance_RestoresTextNumberAndDateTime()
    {
        var store = TestPageInputStores.Create();
        var target = new DateTime(2024, 5, 6, 7, 8, 9);
        var first = new UnixTimestampPageViewModel(new TestMessageService(), store);
        first.TimestampSeconds = "1700000000";
        first.TimestampMilliseconds = "1700000000123";
        first.TargetDateTime = target;
        store.FlushPendingPagesForTests();

        // 页面实例经 _pageCache 缓存复用，这里用新实例模拟应用重启后首次进入页面
        var second = new UnixTimestampPageViewModel(new TestMessageService(), store);

        Assert.Equal("1700000000", second.TimestampSeconds);
        Assert.Equal("1700000000123", second.TimestampMilliseconds);
        Assert.Equal(target, second.TargetDateTime);
        Assert.Equal(target.Date, second.TargetDateSelection);
        Assert.Equal(target.TimeOfDay, second.TargetTimeSelection);
    }

    [Fact]
    public void RestoreAssignment_DoesNotSubmitNewSnapshot()
    {
        // 通过服务本身预置存档，规避测试手写 JSON 与源生成命名策略漂移；
        // 可空选择键存在但值为 null，按可空默认（null）处理
        var store = TestPageInputStores.Create();
        store.Save(PageId, new Dictionary<string, string?>
        {
            ["TimestampSeconds"] = "0",
            ["TargetDateTime"] = "2024-05-06T07:08:09.0000000",
            ["TargetDateSelection"] = null,
            ["TargetTimeSelection"] = null,
        });
        store.FlushPendingPagesForTests();

        var viewModel = new UnixTimestampPageViewModel(new TestMessageService(), store);

        Assert.Equal("0", viewModel.TimestampSeconds);
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), viewModel.TargetDateTime);
        Assert.Null(viewModel.TargetDateSelection);
        Assert.Null(viewModel.TargetTimeSelection);

        // 恢复赋值被 _isRestoring 拦截，不应产生新的待写页（否则恢复即回写）
        Assert.Equal(0, store.PendingPageCountForTests);
    }
}
