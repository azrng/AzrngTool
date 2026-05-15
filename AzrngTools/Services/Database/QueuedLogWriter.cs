using System.Collections.Concurrent;

namespace AzrngTools.Services.Database;

public sealed class QueuedLogWriter : IAsyncDisposable
{
    private readonly Func<string, Task> _writeAsync;
    private readonly ConcurrentQueue<string> _entries = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private int _pendingCount;
    private bool _disposed;

    public QueuedLogWriter(Func<string, Task> writeAsync)
    {
        _writeAsync = writeAsync;
        _worker = Task.Run(ProcessQueueAsync);
    }

    public void Enqueue(string entry)
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Increment(ref _pendingCount);
        _entries.Enqueue(entry);
        _signal.Release();
    }

    public async Task FlushAsync(TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;
        while (Volatile.Read(ref _pendingCount) > 0)
        {
            if (DateTime.UtcNow - startedAt > timeout)
            {
                throw new TimeoutException("Timed out waiting for log queue to flush.");
            }

            await Task.Delay(10);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await FlushAsync(TimeSpan.FromSeconds(2));
        _disposed = true;
        _shutdown.Cancel();
        _signal.Release();

        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
        }

        _shutdown.Dispose();
        _signal.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            await _signal.WaitAsync(_shutdown.Token);
            while (_entries.TryDequeue(out var entry))
            {
                try
                {
                    await _writeAsync(entry);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"写入日志失败：{ex.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingCount);
                }
            }
        }
    }
}
