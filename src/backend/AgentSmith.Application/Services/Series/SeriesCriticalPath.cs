using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207e: the longest chain of tickets not landed yet, through their requires edges — what the series' end
/// waits on — and how long it has left once two tickets landed: the chain's length times the mean time a landed ticket
/// took from its first start. With fewer landings there is no measure, so no estimate.
/// </summary>
public static class SeriesCriticalPath
{
    public static IReadOnlyList<SeriesTicketFact> Of(IReadOnlyList<SeriesTicketFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var open = facts.Where(f => !f.Landed).ToDictionary(f => f.Ticket.TicketId, StringComparer.Ordinal);
        var memo = new Dictionary<string, IReadOnlyList<SeriesTicketFact>>(StringComparer.Ordinal);
        return open.Values.Select(f => Chain(f, open, memo, [])).OrderByDescending(c => c.Count).FirstOrDefault() ?? [];
    }

    public static TimeSpan? Left(IReadOnlyList<SeriesTicketFact> facts, int chain)
    {
        var took = facts.Where(f => f is { LandedAt: not null, FirstStartedAt: not null })
            .Select(f => f.LandedAt!.Value - f.FirstStartedAt!.Value).ToList();
        return took.Count < 2 || chain == 0 ? null : TimeSpan.FromTicks((long)took.Average(t => t.Ticks) * chain);
    }

    // The chain ending at this ticket: its longest unlanded prerequisite's chain, then the ticket itself.
    private static IReadOnlyList<SeriesTicketFact> Chain(SeriesTicketFact fact, IReadOnlyDictionary<string, SeriesTicketFact> open,
        Dictionary<string, IReadOnlyList<SeriesTicketFact>> memo, HashSet<string> visiting)
    {
        if (memo.TryGetValue(fact.Ticket.TicketId, out var known)) return known;
        if (!visiting.Add(fact.Ticket.TicketId)) return [];
        var before = fact.Requires.Where(open.ContainsKey).Select(id => Chain(open[id], open, memo, visiting))
            .OrderByDescending(c => c.Count).FirstOrDefault() ?? [];
        visiting.Remove(fact.Ticket.TicketId);
        return memo[fact.Ticket.TicketId] = [.. before, fact];
    }
}
