using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class QueuedLogWriterTests
{
    [Fact]
    public async Task FlushAsync_writes_enqueued_entries_in_order()
    {
        var entries = new List<string>();
        await using var writer = new QueuedLogWriter(entry =>
        {
            entries.Add(entry);
            return Task.CompletedTask;
        });

        writer.Enqueue("first");
        writer.Enqueue("second");

        await writer.FlushAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["first", "second"], entries);
    }
}
