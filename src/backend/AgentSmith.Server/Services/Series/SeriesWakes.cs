using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-25-9992g: records an event on a member ticket on every active or paused series that holds it, through the
/// store's update, which re-applies the append when another writer saved first. The server only records; the series'
/// next beat hands the wakes to its master, which decides. 2026-11-25-96aec: a wake with a once-key the series has raised
/// already is not appended again (<see cref="SeriesRunRecord.Raised"/>).
/// </summary>
public sealed class SeriesWakes(ISeriesRunStore store, TimeProvider clock, ILogger<SeriesWakes> logger) : ISeriesWakes
{
    public async Task<bool> WakeAsync(string project, string ticketId, SeriesWake wake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wake);
        var holders = (await store.ListAsync([project], cancellationToken)) // the ticket's own project only
            .Where(r => r.IsActive && r.Tickets.Any(t => t.TicketId == ticketId)).ToList();
        var stamped = wake with { At = clock.GetUtcNow(), TicketId = ticketId };
        // 2026-11-27-3532b: a once-key the series raised already writes nothing — a caller may repeat it every sweep
        foreach (var series in holders.Where(h => wake.Once is not { } once || !h.Raised.Contains(once, StringComparer.Ordinal)))
            await store.UpdateAsync(series.Id, r => Recorded(r, stamped), cancellationToken);
        if (holders.Count > 0)
            logger.LogInformation("Series {Series}: woken by {Kind} on ticket {Ticket}",
                string.Join(", ", holders.Select(h => h.Id)), wake.Kind, ticketId);
        return holders.Count > 0;
    }

    private static SeriesRunRecord Recorded(SeriesRunRecord record, SeriesWake wake) => wake.Once is not { } once
        ? record with { Wakes = Appended(record.Wakes, wake) }
        : record.Raised.Contains(once, StringComparer.Ordinal) ? record
        : record with { Wakes = Appended(record.Wakes, wake), Raised = [.. record.Raised, once] };

    private static IReadOnlyList<SeriesWake> Appended(IReadOnlyList<SeriesWake> wakes, SeriesWake wake) =>
        wakes.Any(w => w.SameAs(wake)) ? wakes : [.. wakes, wake];
}
