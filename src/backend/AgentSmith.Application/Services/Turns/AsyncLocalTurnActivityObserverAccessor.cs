using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// AsyncLocal-backed <see cref="ITurnActivityObserverAccessor"/>. A value set before a turn's
/// pipeline runs flows into every tool call, model call and re-prompt that pipeline makes, and
/// is not seen by any run on another flow — a coding run started beside a design turn reports
/// nothing.
/// </summary>
public sealed class AsyncLocalTurnActivityObserverAccessor : ITurnActivityObserverAccessor
{
    private static readonly AsyncLocal<ITurnActivityObserver?> Frame = new();

    public ITurnActivityObserver? Current => Frame.Value;

    public IDisposable Observe(ITurnActivityObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var previous = Frame.Value;
        Frame.Value = observer;
        return new Restore(previous);
    }

    /// <summary>Restores the enclosing observer, so a nested frame unwinds rather than clears.</summary>
    private sealed class Restore(ITurnActivityObserver? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Frame.Value = previous;
        }
    }
}
