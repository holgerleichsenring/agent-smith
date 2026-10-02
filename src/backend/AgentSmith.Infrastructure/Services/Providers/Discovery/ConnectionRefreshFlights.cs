using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// 2026-10-02-5f89c: one refresh per connection at a time in this process. The glob expander's
/// cold refresh, the studio endpoint's and the sweep's share the in-flight task; a caller that
/// gives up stops waiting, not the refresh.
/// <para>
/// A refresh never joins itself: discovery reads a token, the server's secret values may reload
/// the configuration, and that load can expand the same connection's globs — which would
/// otherwise wait on the very task it runs inside. On the refresh's own flow the work runs
/// directly.
/// </para>
/// </summary>
public sealed class ConnectionRefreshFlights
{
    private readonly ConcurrentDictionary<string, Lazy<Task>> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<ImmutableHashSet<string>?> _onThisFlow = new();

    public Task RunAsync(string connectionName, Func<Task> refresh, CancellationToken cancellationToken)
    {
        if (_onThisFlow.Value?.Contains(connectionName) == true) return refresh();
        var flight = _inFlight.GetOrAdd(connectionName, name => new Lazy<Task>(() => FlyAsync(name, refresh)));
        return flight.Value.WaitAsync(cancellationToken);
    }

    private async Task FlyAsync(string connectionName, Func<Task> refresh)
    {
        await Task.Yield();
        _onThisFlow.Value = (_onThisFlow.Value ?? ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase))
            .Add(connectionName);
        try
        {
            await refresh();
        }
        finally
        {
            _inFlight.TryRemove(connectionName, out _);
        }
    }
}
