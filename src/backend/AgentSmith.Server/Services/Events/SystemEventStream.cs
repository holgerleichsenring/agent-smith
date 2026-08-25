using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// The SYSTEM event stream — poll cycles, config reads, webhook traffic — with its own ring
/// buffer, its own cursor and its own fanout. It shares only a Redis connection with the run
/// streams; extracted from <see cref="JobsBroadcaster"/> (2026-08-24-ca23), which had grown to
/// carry both and could not take another responsibility without shedding one.
/// </summary>
public sealed class SystemEventStream(IRunEventFanout fanout, EventEnvelopeSerializer envelopes)
{
    // p0175-fix: bumped from 500 → 10_000 to match the Redis system stream MAXLEN. The 500-cap
    // was too tight for active trackers — one poller pushing ~94 events/cycle exhausted the
    // buffer in 5-6 cycles, and the 24h rollup then diverged from the visible cycle list
    // because the oldest PollCycleStarted got evicted while its matching Finished did not.
    private const int Capacity = 10_000;

    private readonly SystemRecentRingBuffer _recent = new(Capacity);
    private string _cursor = "0-0";

    public IReadOnlyList<SystemEvent> Recent => _recent.Snapshot();

    /// <summary>
    /// p0175-fix: 24h rolling aggregate over the in-memory ring. Cheap O(N); called on each
    /// subscribe and re-broadcast after every system event publish.
    /// </summary>
    public SystemActivitySnapshot Activity() =>
        SystemActivitySnapshot.Compute(_recent.Snapshot(), DateTimeOffset.UtcNow);

    /// <summary>
    /// p0173a: cold-start populates the ring from the newest entries so the dashboard's system
    /// view is immediately useful after a restart.
    /// </summary>
    public async Task RehydrateAsync(IDatabase db, CancellationToken ct)
    {
        if (!await db.KeyExistsAsync(SystemEventStreamKeys.Stream)) return;
        var entries = await db.StreamRangeAsync(
            SystemEventStreamKeys.Stream, "-", "+", Capacity, Order.Descending);
        // Order.Descending returns newest-first; reverse to append chronologically.
        for (var i = entries.Length - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            var systemEvent = Deserialize(entries[i]);
            if (systemEvent is not null) _recent.Append(systemEvent);
        }
        // p0258: anchor the live drain at the REAL last stream id (entries are newest-first, so
        // entries[0] is it) — NOT the "$" sentinel, which converted to "0-0" and re-read the
        // whole stream on every restart, re-fanning every historical event to SignalR.
        if (entries.Length > 0) _cursor = entries[0].Id.ToString();
    }

    public async Task DrainAsync(IDatabase db, CancellationToken ct)
    {
        var entries = await db.StreamReadAsync(SystemEventStreamKeys.Stream, _cursor, count: 100);
        if (entries.Length == 0) return;
        var appended = false;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var systemEvent = Deserialize(entry);
            if (systemEvent is null) continue;
            _recent.Append(systemEvent);
            appended = true;
            await fanout.ToSystemAsync(systemEvent, ct);
        }
        _cursor = entries[^1].Id.ToString();
        // p0175-fix: one rollup broadcast per batch (not per event) keeps the SignalR overhead
        // bounded under burst load while still keeping the KPI cards within one loop interval.
        if (appended) await fanout.ToSystemActivityAsync(Activity(), ct);
    }

    private SystemEvent? Deserialize(StreamEntry entry)
    {
        foreach (var pair in entry.Values)
        {
            var payload = pair.Value.ToString();
            if (string.IsNullOrEmpty(payload)) continue;
            try { return envelopes.DeserializeSystem(payload); }
            catch { return null; }
        }
        return null;
    }
}
