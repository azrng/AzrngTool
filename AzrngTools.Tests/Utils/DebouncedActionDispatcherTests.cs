using AzrngTools.Utils;

namespace AzrngTools.Tests.Utils;

public class DebouncedActionDispatcherTests
{
    [Fact]
    public async Task Debounce_executes_only_latest_action_after_delay()
    {
        using var dispatcher = new DebouncedActionDispatcher(TimeSpan.FromMilliseconds(40));
        var executed = new List<string>();

        dispatcher.Debounce(() => executed.Add("first"));
        dispatcher.Debounce(() => executed.Add("second"));
        dispatcher.Debounce(() => executed.Add("third"));

        await Task.Delay(120);

        var value = Assert.Single(executed);
        Assert.Equal("third", value);
    }
}
