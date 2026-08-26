using AgentSmith.Contracts.Models.Access;
using AgentSmith.Server.Contracts;
using System.Collections.Concurrent;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-26-7a51: the in-memory coalescer between the authorization path and the
/// observed-caller table.
/// <para>
/// A row per validated token would be one upsert per request per caller — on SQLite, the
/// default, that is the write lock taken on the authorization path, with the dashboard's
/// polling and the hub handshake multiplying it. So an observation lands in a dictionary
/// keyed by subject, a caller already noted inside the window is dropped outright, and a
/// hosted service drains what is left off the request path.
/// </para>
/// </summary>
internal sealed class CallerObservationBuffer(TimeProvider clock) : ICallerObservations
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, ObservedCaller> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _noted = new(StringComparer.Ordinal);

    public void Observe(ObservedCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var now = clock.GetUtcNow();
        if (_noted.TryGetValue(caller.Subject, out var last) && now - last < Window) return;
        _noted[caller.Subject] = now;
        _pending[caller.Subject] = caller;
    }

    /// <summary>Everything noted since the last drain, and the buffer is empty afterwards.</summary>
    public IReadOnlyList<ObservedCaller> Drain()
    {
        var drained = _pending.Keys.ToList();
        return [.. drained.Select(subject => _pending.TryRemove(subject, out var caller) ? caller : null)
            .OfType<ObservedCaller>()];
    }

    /// <summary>
    /// The window has to be forgotten when a write fails, or a caller suppressed by a
    /// flush that never landed would not be written again for as long as they keep calling.
    /// </summary>
    public void Reinstate(IReadOnlyList<ObservedCaller> callers)
    {
        ArgumentNullException.ThrowIfNull(callers);
        foreach (var caller in callers)
        {
            _noted.TryRemove(caller.Subject, out _);
            _pending.TryAdd(caller.Subject, caller);
        }
    }
}
