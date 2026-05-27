using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
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
public sealed class TrailReader(IConnectionMultiplexer redis)
{
    public const int DefaultPageCount = 500;
    public const int MaxPageCount = 2000;

    public async Task<IReadOnlyList<object>> ReadAllAsync(string runId)
    {
        var db = redis.GetDatabase();
        var entries = await db.StreamRangeAsync(EventStreamKeys.RunStream(runId), "-", "+");
        return ToEvents(entries);
    }

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
        return ToTypedEvents(entries);
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
