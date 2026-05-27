using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Server-side rollup of the system event stream over a rolling 24h window.
/// Single source of truth for the /system "Last 24h" and "By source" cards —
/// the dashboard reads these counters instead of deriving them from a
/// capped local event buffer (which drifted under load: when the client
/// buffer evicted the oldest PollCycleStarted but not its matching
/// Finished, the KPI showed N cycles while the visible list reconstructed
/// only N-1).
/// </summary>
public sealed record SystemActivitySnapshot(
    int TicketsScanned,
    int TicketsTriggered,
    int TicketsSkipped,
    int WebhooksReceived,
    int WebhooksActioned,
    int PollCyclesStarted,
    int PollCyclesFinished,
    IReadOnlyDictionary<string, int> EventsPerSource)
{
    public static SystemActivitySnapshot Empty { get; } = new(
        0, 0, 0, 0, 0, 0, 0,
        new Dictionary<string, int>(StringComparer.Ordinal));

    public static SystemActivitySnapshot Compute(
        IReadOnlyList<SystemEvent> events, DateTimeOffset now)
    {
        var cutoff = now - TimeSpan.FromHours(24);
        var ticketsScanned = 0;
        var ticketsTriggered = 0;
        var ticketsSkipped = 0;
        var webhooksReceived = 0;
        var webhooksActioned = 0;
        var pollCyclesStarted = 0;
        var pollCyclesFinished = 0;
        var perSource = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var e in events)
        {
            if (e.Timestamp < cutoff) continue;
            perSource[e.Source] = perSource.TryGetValue(e.Source, out var c) ? c + 1 : 1;
            switch (e.Type)
            {
                case SystemEventType.TicketScanned: ticketsScanned++; break;
                case SystemEventType.TicketTriggered: ticketsTriggered++; break;
                case SystemEventType.TicketSkipped: ticketsSkipped++; break;
                case SystemEventType.WebhookReceived:
                    webhooksReceived++;
                    if (e is WebhookReceivedEvent w && w.Actioned) webhooksActioned++;
                    break;
                case SystemEventType.PollCycleStarted: pollCyclesStarted++; break;
                case SystemEventType.PollCycleFinished: pollCyclesFinished++; break;
            }
        }

        return new SystemActivitySnapshot(
            ticketsScanned, ticketsTriggered, ticketsSkipped,
            webhooksReceived, webhooksActioned,
            pollCyclesStarted, pollCyclesFinished,
            perSource);
    }
}
