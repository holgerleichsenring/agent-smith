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
    private readonly SemaphoreSlim _gate = new(1, 1);

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
    /// 2026-09-14-2b7c: a person who has been removed is forgotten HERE too, or the note that
    /// suppresses their next observation outlives the row and the grant that went with it —
    /// and they stay off the surface for up to five minutes, through any number of sign-ins,
    /// because the suppression is keyed by subject and lives in this process.
    /// <para>
    /// Both maps, not just the note: an entry still in <c>_pending</c> would be written back
    /// by the next drain, re-inserting the person as an observation from before they were
    /// forgotten.
    /// </para>
    /// </summary>
    public void Forget(string subject)
    {
        _noted.TryRemove(subject, out _);
        _pending.TryRemove(subject, out _);
    }

    /// <summary>
    /// 2026-09-14-2b7c: held around drain-and-write on one side and forget-and-delete on the
    /// other, because ordering alone closes nothing: <see cref="Drain"/> moves entries into a
    /// local list inside the flush service, where no forget can reach them, so a batch in
    /// flight can write a row back after the removal deleted it — with the caller having made
    /// no request at all.
    /// <para>
    /// <see cref="Observe"/> never takes it, and must not: noting a caller costs a dictionary
    /// write and returns, which is the whole reason this buffer exists. The maps are
    /// concurrent, so an observation racing a removal is safe, and its outcome is the right
    /// one — somebody still making requests belongs back on the surface.
    /// </para>
    /// </summary>
    public async Task<IDisposable> HoldAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Release(_gate);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            gate.Release();
        }
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
