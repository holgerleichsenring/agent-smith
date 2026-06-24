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
    /// p0288: the execution-tree source. ALWAYS reads the durable DB trail — the
    /// structural skeleton (RunStarted + every StepStarted/Finished + LLM /
    /// sandbox / decision / PR events) the dashboard needs to draw the rail.
    /// Excludes the high-volume live SandboxOutput (kept Redis-only, by design),
    /// so it is small (≈ one row per structural event) and complete regardless of
    /// Redis TTL, flush, or the client's per-run cap. Object-typed for the hub so
    /// STJ serialises each element by its runtime subtype (see p0175 note above).
    /// </summary>
    public async Task<IReadOnlyList<object>> ReadDbTrailAsync(string runId) =>
        (await ReadDbTrailTypedAsync(runId)).Cast<object>().ToList();

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

    private static IReadOnlyList<object> ToEvents(StreamEntry[] entries) =>
        ToTypedEvents(entries).Cast<object>().ToList();

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
