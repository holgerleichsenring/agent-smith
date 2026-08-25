using System.Collections.Concurrent;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Services.Events;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-08-24-ca23: where the drain stands in each run's stream. In memory for the live loop,
/// and in Redis for a run that PAUSES — parked on a question or requeued for capacity — because
/// a pause is not an ending and the same run id relaunches with its whole history still behind
/// it. Without a kept position the drain re-reads that history and every consumer downstream
/// records it again.
/// <para>
/// The stored position deliberately outlives the stream it points into, which is what makes the
/// fallback correct rather than lucky: no stored position implies no stream to skip. The reverse
/// is harmless — stream ids are time-ordered, so a stale position precedes every new entry.
/// </para>
/// </summary>
public sealed class RunStreamCursors
{
    private const string StreamStart = "0-0";
    private readonly ConcurrentDictionary<string, string> _positions = new(StringComparer.Ordinal);

    public bool IsTracked(string runId) => _positions.ContainsKey(runId);

    public (string RunId, string Position)[] Tracked() =>
        _positions.Select(p => (p.Key, p.Value)).ToArray();

    /// <summary>Move to an entry that has been processed; nothing durable to record.</summary>
    public void Advance(string runId, string entryId) => _positions[runId] = entryId;

    /// <summary>Begin tracking a run, continuing from its stored position when it has one.</summary>
    public async Task TrackAsync(IDatabase db, string runId)
    {
        var stored = await db.StringGetAsync(EventStreamKeys.RunCursor(runId));
        _positions[runId] = stored.HasValue ? stored.ToString() : StreamStart;
    }

    /// <summary>Track a run at a position established elsewhere (a cold-start anchor).</summary>
    public void TrackAt(string runId, string entryId) => _positions[runId] = entryId;

    /// <summary>
    /// The drain stopped at a run's terminal entry. A PAUSE moves the position PAST that entry
    /// and records it, so the relaunch continues after it — and so the wait itself does not
    /// re-read that one entry on every poll, which the drain's break-before-advance would
    /// otherwise cause. A real ending forgets the run entirely.
    /// </summary>
    public async Task StopAtAsync(IDatabase db, string runId, string entryId, bool isPause)
    {
        if (isPause)
        {
            _positions[runId] = entryId;
            await db.StringSetAsync(EventStreamKeys.RunCursor(runId), entryId, EventStreamKeys.CursorTtl);
            return;
        }
        _positions.TryRemove(runId, out _);
        await db.KeyDeleteAsync(EventStreamKeys.RunCursor(runId));
    }

    /// <summary>
    /// At boot, give a position to the runs the STORE calls unfinished but the active set does
    /// not list — a run that paused left that set, so no rehydration from Redis can see it. A
    /// live stream with no recorded position is anchored at the TAIL: skipping history is the
    /// p0258 stance, replaying it is the defect this exists to prevent.
    /// </summary>
    public async Task AnchorUnfinishedAsync(
        IDatabase db, IUnfinishedRunSource source, CancellationToken ct)
    {
        foreach (var runId in await source.GetUnfinishedRunIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            if (IsTracked(runId)) continue;
            var key = EventStreamKeys.RunStream(runId);
            if (!await db.KeyExistsAsync(key)) continue;
            var stored = await db.StringGetAsync(EventStreamKeys.RunCursor(runId));
            if (stored.HasValue) { TrackAt(runId, stored.ToString()); continue; }
            var tail = await db.StreamRangeAsync(key, "-", "+", 1, Order.Descending);
            if (tail.Length > 0) TrackAt(runId, tail[0].Id.ToString());
        }
    }
}
