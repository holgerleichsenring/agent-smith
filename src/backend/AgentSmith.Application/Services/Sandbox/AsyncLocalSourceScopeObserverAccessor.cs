using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// AsyncLocal-backed <see cref="ISourceScopeObserverAccessor"/>. A value set before a
/// pipeline runs flows down into every scope that pipeline materialises, and is not seen by
/// any run on another flow — a coding run started beside a design turn reports nothing.
/// </summary>
public sealed class AsyncLocalSourceScopeObserverAccessor : ISourceScopeObserverAccessor
{
    private static readonly AsyncLocal<ISourceScopeObserver?> Frame = new();

    public ISourceScopeObserver? Current => Frame.Value;

    public IDisposable Observe(ISourceScopeObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var previous = Frame.Value;
        Frame.Value = observer;
        return new Restore(previous);
    }

    /// <summary>Restores the enclosing observer, so a nested frame unwinds rather than clears.</summary>
    private sealed class Restore(ISourceScopeObserver? previous) : IDisposable
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
