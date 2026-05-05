using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class OutputBatcher : IAsyncDisposable
{
    public const int DefaultThresholdCount = 50;
    public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMilliseconds(100);

    private readonly int _thresholdCount;
    private readonly Func<IReadOnlyList<StepEvent>, Task> _onFlush;
    private readonly List<StepEvent> _buffer = new(64);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly Timer _timer;
    private readonly object _bufferLock = new();
    private bool _disposed;

    public OutputBatcher(
        int thresholdCount,
        TimeSpan flushInterval,
        Func<IReadOnlyList<StepEvent>, Task> onFlush)
    {
        _thresholdCount = thresholdCount;
        _onFlush = onFlush;
        _timer = new Timer(_ => _ = FlushAsync(), null, flushInterval, flushInterval);
    }

    public void Add(StepEvent ev)
    {
        bool shouldFlush;
        lock (_bufferLock)
        {
            _buffer.Add(ev);
            shouldFlush = _buffer.Count >= _thresholdCount;
        }
        if (shouldFlush)
        {
            _ = FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        await _flushLock.WaitAsync();
        try
        {
            StepEvent[] snapshot;
            lock (_bufferLock)
            {
                if (_buffer.Count == 0) return;
                snapshot = _buffer.ToArray();
                _buffer.Clear();
            }
            await _onFlush(snapshot);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _timer.DisposeAsync();
        await FlushAsync();
        _flushLock.Dispose();
    }
}
