namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-23-31dcb: the order of a series — each ticket's depth (the longest requires path to it inside the series, 0
/// without one) and the merge order: the tickets with an open pull request, not landed, by depth then series order. One
/// step at a time is ready — the first whose requirements all landed and that targets the default branch; a later one
/// that could merge waits after it, one with an unlanded requirement waits after those, one off the default branch waits
/// for its retarget.
/// </summary>
public sealed class SeriesRunOrder
{
    public IReadOnlyDictionary<string, int> Depths(IReadOnlyList<SeriesRunOrderTicket> tickets)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        var byId = tickets.ToDictionary(t => t.TicketId, StringComparer.Ordinal);
        var memo = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ticket in tickets) Depth(ticket, byId, memo, []);
        return memo;
    }

    public IReadOnlyList<SeriesRunMergeStep> MergeOrder(IReadOnlyList<SeriesRunOrderTicket> tickets, IReadOnlySet<string> landed)
    {
        ArgumentNullException.ThrowIfNull(landed);
        var depths = Depths(tickets);
        var keys = tickets.ToDictionary(t => t.TicketId, t => t.Key, StringComparer.Ordinal);
        string? ready = null;
        var steps = new List<SeriesRunMergeStep>();
        foreach (var ticket in tickets.Where(t => t.HasOpenPullRequest && !landed.Contains(t.TicketId))
                     .Select((t, index) => (t, index)).OrderBy(x => depths[x.t.TicketId]).ThenBy(x => x.index).Select(x => x.t))
        {
            var step = Step(ticket, depths[ticket.TicketId], landed, keys, ready);
            if (step.Wait == SeriesRunMergeWaits.Ready) ready = ticket.Key;
            steps.Add(step);
        }
        return steps;
    }

    private static SeriesRunMergeStep Step(SeriesRunOrderTicket ticket, int depth, IReadOnlySet<string> landed,
        IReadOnlyDictionary<string, string> keys, string? ready)
    {
        var unlanded = ticket.Requires.Where(r => !landed.Contains(r)).Select(r => keys.GetValueOrDefault(r) ?? r).ToList();
        if (unlanded.Count > 0) return new(ticket.TicketId, ticket.Key, depth, SeriesRunMergeWaits.After, unlanded);
        if (ticket.TargetsElsewhere) return new(ticket.TicketId, ticket.Key, depth, SeriesRunMergeWaits.Retarget, []);
        return ready is null
            ? new(ticket.TicketId, ticket.Key, depth, SeriesRunMergeWaits.Ready, [])
            : new(ticket.TicketId, ticket.Key, depth, SeriesRunMergeWaits.After, [ready]);
    }

    // The longest requires path inside the series; an edge leaving the series counts nothing, a cycle is cut where it closes.
    private static int Depth(SeriesRunOrderTicket ticket, IReadOnlyDictionary<string, SeriesRunOrderTicket> byId,
        Dictionary<string, int> memo, HashSet<string> visiting)
    {
        if (memo.TryGetValue(ticket.TicketId, out var known)) return known;
        if (!visiting.Add(ticket.TicketId)) return 0;
        var depth = ticket.Requires.Where(byId.ContainsKey).Where(r => !visiting.Contains(r))
            .Select(r => Depth(byId[r], byId, memo, visiting) + 1).DefaultIfEmpty(0).Max();
        visiting.Remove(ticket.TicketId);
        return memo[ticket.TicketId] = depth;
    }
}
