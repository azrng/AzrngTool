using AzrngTools.Utils;

namespace AzrngTools.Tests.Utils;

public class DebouncedActionDispatcherTests
{
    [Fact]
    public async Task Debounce_executes_only_latest_action_after_delay()
    {
        using var dispatcher = new DebouncedActionDispatcher(TimeSpan.FromMilliseconds(40));
        var executed = new List<string>();
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Record(string value)
        {
            executed.Add(value);
            completion.TrySetResult(value);
        }

        dispatcher.Debounce(() => Record("first"));
        dispatcher.Debounce(() => Record("second"));
        dispatcher.Debounce(() => Record("third"));

        var value = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("third", value);
        Assert.Single(executed);
    }
}
