using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532b: the series one resume sweep reads — each project's series once (<see cref="ISeriesRunStore.ListAsync"/>),
/// however many of its runs are parked; a sweep makes a new one, so the next sweep reads them fresh.
/// </summary>
public sealed class SeriesLedScan(ISeriesRunStore store)
{
    private readonly Dictionary<string, IReadOnlyList<SeriesRunRecord>> _byProject = new(StringComparer.Ordinal);

    /// <summary>The newest series of the project that holds the ticket, active or ended; null when none ever did.</summary>
    public async Task<SeriesRunRecord?> NewestAsync(string project, string ticketId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(ticketId)) return null;
        if (!_byProject.TryGetValue(project, out var records)) _byProject[project] = records = await store.ListAsync([project], ct);
        return records.Where(r => r.Tickets.Any(t => t.TicketId == ticketId)).MaxBy(r => r.StartedAt);
    }
}
