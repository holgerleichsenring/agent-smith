using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0173a / p0248: the bounded recent tail of the system event stream that a caller
/// joining the system group is seeded with. Extracted from <see cref="JobsHub"/> so the
/// hub carries the subscription and this carries the read.
///
/// <para>The tracker is a live "what is the poller doing right now" view, not an audit
/// log — the durable signal (a triggered ticket) is a Run in the DB. So a join seeds only
/// a small recent tail for immediate context; live events then arrive via JobsBroadcaster's
/// stream drain. The full retained window (up to MAXLEN / 24h) is deliberately NOT
/// replayed: dumping the whole history on every (re)subscribe is the "runs through
/// everything again" effect, and nobody scrolls a poll log back hours.</para>
/// </summary>
public sealed class SystemBacklogReader(
    IConnectionMultiplexer redis, EventEnvelopeSerializer envelopes)
{
    private const int Tail = 50;

    /// <summary>
    /// The last <see cref="Tail"/> system events, oldest first. List&lt;object&gt;, not
    /// List&lt;SystemEvent&gt;: SignalR/STJ serializes each element by its DECLARED type,
    /// so a typed list would drop every derived property (tracker, labels, ticketId, …)
    /// and crash the client. Boxing to object makes STJ emit the concrete runtime type —
    /// the same reason GetTrail returns IReadOnlyList&lt;object&gt;.
    /// </summary>
    public async Task<IReadOnlyList<object>> ReadAsync()
    {
        // Last N descending, then reverse to chronological order for the client.
        var entries = await redis.GetDatabase().StreamRangeAsync(
            SystemEventStreamKeys.Stream, "-", "+", Tail, Order.Descending);

        var backlog = new List<object>(entries.Length);
        for (var i = entries.Length - 1; i >= 0; i--)
            backlog.AddRange(Read(entries[i]));
        return backlog;
    }

    private IEnumerable<object> Read(StreamEntry entry)
    {
        foreach (var pair in entry.Values)
        {
            var payload = pair.Value.ToString();
            if (string.IsNullOrEmpty(payload)) continue;
            var systemEvent = envelopes.DeserializeSystem(payload);
            if (systemEvent is not null) yield return systemEvent;
        }
    }
}
