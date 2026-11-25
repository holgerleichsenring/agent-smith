using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-25-9992g: records an event on a member ticket on every active or paused series that holds it, through the
/// store's update, which re-applies the append when another writer saved first. The server only records; the series'
/// next beat hands the wakes to its master, which decides.
/// </summary>
public sealed class SeriesWakes(ISeriesRunStore store, TimeProvider clock, ILogger<SeriesWakes> logger) : ISeriesWakes
{
    public async Task<bool> WakeAsync(string project, string ticketId, SeriesWake wake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wake);
        var holders = (await store.ListAsync([project], cancellationToken)) // the ticket's own project only
            .Where(r => r.IsActive && r.Tickets.Any(t => t.TicketId == ticketId)).ToList();
        var stamped = wake with { At = clock.GetUtcNow(), TicketId = ticketId };
        foreach (var series in holders)
            await store.UpdateAsync(series.Id, r => r with { Wakes = Appended(r.Wakes, stamped) }, cancellationToken);
        if (holders.Count > 0)
            logger.LogInformation("Series {Series}: woken by {Kind} on ticket {Ticket}",
                string.Join(", ", holders.Select(h => h.Id)), wake.Kind, ticketId);
        return holders.Count > 0;
    }

    private static IReadOnlyList<SeriesWake> Appended(IReadOnlyList<SeriesWake> wakes, SeriesWake wake) =>
        wakes.Any(w => w.SameAs(wake)) ? wakes : [.. wakes, wake];
}
