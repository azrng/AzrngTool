namespace AzrngTools.Utils;

public sealed class DebouncedActionDispatcher : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _pendingAction;
    private bool _disposed;

    public DebouncedActionDispatcher(TimeSpan delay)
    {
        _delay = delay;
    }

    public void Debounce(Action action)
    {
        CancellationTokenSource cancellation;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _pendingAction?.Cancel();
            _pendingAction = new CancellationTokenSource();
            cancellation = _pendingAction;
        }

        _ = RunAsync(action, cancellation.Token);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingAction?.Cancel();
            _pendingAction?.Dispose();
            _pendingAction = null;
        }
    }

    private async Task RunAsync(Action action, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                action();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }
}
