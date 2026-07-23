using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0169h: reads the per-run event stream into a flat list (full window or
/// paginated slice). Extracted from JobsHub so it can be unit-tested without
/// the SignalR connection plumbing.
///
/// <para>p0175-fix: <see cref="ReadAllAsync"/> + <see cref="ReadPageAsync"/>
/// return <c>IReadOnlyList&lt;object&gt;</c> so System.Text.Json serialises
/// each element using its RUNTIME type — concrete subclass fields
/// (StepIndex, StepName, …) appear on the wire. The previous typed
/// collection caused STJ to use the declared element type (the
/// <see cref="RunEvent"/> base) and drop every derived field, surfacing as
/// "undefined / NaNs" on the dashboard's Trail tab. The cast is purely a
/// serialisation hint — runtime elements remain RunEvent subclasses, and
/// the <see cref="ReadAllTypedAsync"/> seam exposes them strongly-typed
/// for unit tests.</para>
/// </summary>
public sealed class TrailReader(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory)
{
    public const int DefaultPageCount = 500;
    public const int MaxPageCount = 2000;

    public async Task<IReadOnlyList<object>> ReadAllAsync(string runId) =>
        (await ReadAllTypedAsync(runId)).Cast<object>().ToList();

    public async Task<TrailPage> ReadPageAsync(string runId, string? fromId, int? count)
    {
        var page = Math.Clamp(count ?? DefaultPageCount, 1, MaxPageCount);
        var db = redis.GetDatabase();
        var key = EventStreamKeys.RunStream(runId);
        var entries = await db.StreamRangeAsync(key, fromId ?? "-", "+", page + 1);
        var hasMore = entries.Length > page;
        var slice = hasMore ? entries.Take(page).ToArray() : entries;
        var events = ToEvents(slice);
        var nextCursor = slice.Length > 0 ? slice[^1].Id.ToString() : null;
        return new TrailPage(runId, events, nextCursor, hasMore);
    }

    /// <summary>
    /// Test seam — strongly-typed projection for unit tests that assert on
    /// concrete RunEvent record properties. The hub path uses
    /// <see cref="ReadAllAsync"/> (object list) for the polymorphic-
    /// serialisation reason documented above.
    /// </summary>
    public async Task<IReadOnlyList<RunEvent>> ReadAllTypedAsync(string runId)
    {
        var db = redis.GetDatabase();
        var entries = await db.StreamRangeAsync(EventStreamKeys.RunStream(runId), "-", "+");
        if (entries.Length > 0) return ToTypedEvents(entries);

        // Redis stream gone — 24h TTL expired (p0169j-a) or a flush/restart lost
        // it. Replay the durable DB trail so a finished run keeps its full
        // execution view instead of collapsing to the structured snapshot.
        return await ReadDbTrailTypedAsync(runId);
    }

    /// <summary>
    /// p0291: the execution-rail source for SubscribeRun. Replays the Redis run
    /// stream FILTERED to structural events — everything EXCEPT the high-volume
    /// SandboxOutput stdout, which the broadcaster routes to the sandbox group
    /// (not the run group), so it never belongs on the rail. Redis is written per
    /// event, so this is REAL-TIME and complete even mid-run — unlike the batched
    /// DB trail (p0288), which lags a flush and dropped just-emitted steps from a
    /// live run's rail. When the Redis stream is gone (24h TTL / flush / restart)
    /// it falls back to the durable DB trail (already structural). Object-typed so
    /// STJ serialises each element by its runtime subtype (see p0175 note above).
    /// </summary>
    public async Task<IReadOnlyList<object>> ReadStructuralTrailAsync(string runId)
    {
        var db = redis.GetDatabase();
        var entries = await db.StreamRangeAsync(EventStreamKeys.RunStream(runId), "-", "+");
        if (entries.Length > 0)
            return ToTypedEvents(entries).Where(IsStructural).Cast<object>().ToList();

        return (await ReadDbTrailTypedAsync(runId)).Cast<object>().ToList();
    }

    // Mirrors the broadcaster's run-group filter (JobsBroadcaster.ProcessEventAsync):
    // SandboxOutput fans out to the sandbox group only and never reaches the rail.
    private static bool IsStructural(RunEvent e) => e.Type != EventType.SandboxOutput;

    public async Task<IReadOnlyList<RunEvent>> ReadDbTrailTypedAsync(string runId)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var rows = await uow.Set<Infrastructure.Persistence.Entities.RunEvent>()
            .AsNoTracking()
            .Where(e => e.RunId == runId)
            .OrderBy(e => e.Seq)
            .ToListAsync();

        var events = new List<RunEvent>(rows.Count);
        foreach (var row in rows)
        {
            var ev = EventEnvelopeSerializer.DeserializeRaw(row.Type, row.PayloadJson);
            if (ev is not null) events.Add(ev);
        }
        return events;
    }

    /// <summary>
    /// p0373: the DB-backed structural trail as a Seq-delta page — the pull source
    /// for the dashboard's full-pipeline view. The DB is the system-of-record: it
    /// holds every structural event in order and is NEVER evicted, unlike the Redis
    /// run stream whose MAXLEN window is flooded and rolled over by high-volume
    /// SandboxOutput stdout (which is why the Redis-backed GetTrail collapses to a
    /// stdout-only tail mid-run). stdout is not persisted at all, so this is
    /// structural by construction; the explicit type guard is belt-and-suspenders.
    /// Returns events runtime-typed as object so STJ emits concrete subclass fields
    /// (see the p0175 note above), plus the max Seq seen so the caller polls the
    /// next delta with <paramref name="sinceSeq"/>.
    /// </summary>
    public async Task<DbTrailPage> ReadDbTrailSinceAsync(
        string runId, long sinceSeq, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sandboxOutput = nameof(EventType.SandboxOutput);
        var rows = await uow.Set<Infrastructure.Persistence.Entities.RunEvent>()
            .AsNoTracking()
            .Where(e => e.RunId == runId && e.Seq > sinceSeq && e.Type != sandboxOutput)
            .OrderBy(e => e.Seq)
            .ToListAsync(cancellationToken);

        var events = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            var ev = EventEnvelopeSerializer.DeserializeRaw(row.Type, row.PayloadJson);
            if (ev is not null) events.Add(ev);
        }
        // Advance the cursor past every scanned row even if one failed to
        // deserialize, so a single bad payload can't wedge the poll in a loop.
        var maxSeq = rows.Count > 0 ? rows[^1].Seq : sinceSeq;
        return new DbTrailPage(events, maxSeq);
    }

    private static IReadOnlyList<object> ToEvents(StreamEntry[] entries) =>
        ToTypedEvents(entries).Cast<object>().ToList();

    /// <summary>p0373: a Seq-delta page of the DB structural trail.</summary>
    public sealed record DbTrailPage(IReadOnlyList<object> Events, long MaxSeq);

    private static IReadOnlyList<RunEvent> ToTypedEvents(StreamEntry[] entries)
    {
        var events = new List<RunEvent>(entries.Length);
        foreach (var entry in entries)
        {
            foreach (var pair in entry.Values)
            {
                var payload = pair.Value.ToString();
                if (string.IsNullOrEmpty(payload)) continue;
                var runEvent = EventEnvelopeSerializer.Deserialize(payload);
                if (runEvent is not null) events.Add(runEvent);
            }
        }
        return events;
    }
}
