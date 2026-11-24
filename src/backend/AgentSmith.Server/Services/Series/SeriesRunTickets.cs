using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcb: a series run's tickets from its project's facts — each with its depth, repos, pull requests per repo
/// and newest status — its progress counts and its merge order (<see cref="SeriesRunOrder"/>). An open pull request's
/// target is not stored, so it counts as the default branch here; the merge itself reads the host's base.
/// <para>2026-11-23-31dcg: each ticket carries its hold, read where the ticket list reads it, and the held count is the
/// tickets whose hold names someone and is not the next merge — none when no holds were read (the merge order alone needs none).</para>
/// </summary>
public sealed class SeriesRunTickets(SeriesRunOrder order, SeriesRunPullRequests pullRequests)
{
    public SeriesRunTicketsShape Of(SeriesRunRecord record, SeriesRunProjectFacts facts,
        IReadOnlyDictionary<string, AgentSmith.Application.Services.Holds.TicketHold>? holds = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(facts);
        var prs = record.Tickets.Select(t => t.TicketId).Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => pullRequests.Of(facts.PullRequests[id], facts.Project), StringComparer.Ordinal);
        var ordered = record.Tickets.Select(t => new SeriesRunOrderTicket(t.TicketId, t.Key, facts.Requires.GetValueOrDefault(t.TicketId) ?? [])
        {
            HasOpenPullRequest = prs[t.TicketId].Any(SeriesRunPullRequests.Waits),
            TargetsElsewhere = prs[t.TicketId].Any(p => SeriesRunPullRequests.Waits(p) && pullRequests.TargetsElsewhere(p, facts.Project)),
        }).ToList();
        var depths = order.Depths(ordered);
        var tickets = record.Tickets.Select((t, index) => (View: View(t, ordered[index], depths, facts, prs[t.TicketId])
                with { Hold = holds?.GetValueOrDefault(t.TicketId) }, index))
            .OrderBy(x => x.View.Depth).ThenBy(x => x.index).Select(x => x.View).ToList();
        var merges = Needed(order.MergeOrder(ordered, facts.Landed), tickets, holds);
        return new SeriesRunTicketsShape(tickets, Counts(tickets, facts), merges) { NextMerge = NextMerge(merges, holds) };
    }

    /// <summary>2026-11-23-8d16: each step with the keys of the series' tickets that start only once it merged — read from
    /// their starts_on_merge holds by the rule the hold's merge count reads, so the page marks what the hold counts.</summary>
    internal static IReadOnlyList<SeriesRunMergeStep> Needed(IReadOnlyList<SeriesRunMergeStep> merges,
        IReadOnlyList<SeriesRunTicketView> tickets, IReadOnlyDictionary<string, AgentSmith.Application.Services.Holds.TicketHold>? holds)
    {
        if (holds is null) return merges;
        var by = merges.ToDictionary(s => s.TicketId, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var ticket in tickets)
            if (holds.GetValueOrDefault(ticket.TicketId) is { Case: AgentSmith.Application.Services.Holds.TicketHoldCases.StartsOnMerge } hold)
                foreach (var step in AgentSmith.Application.Services.Holds.TicketSeriesMergeHold.Needed(merges, hold.Names))
                    by[step.TicketId].Add(ticket.Key);
        return [.. merges.Select(s => by[s.TicketId].Count == 0 ? s : s with { NeededBy = by[s.TicketId] })];
    }

    /// <summary>2026-11-23-849e: the ready step's key while it is the person's merge — the one rule the series page, its
    /// list row and the ticket's hold share; without holds the merge order alone decides.</summary>
    internal static string? NextMerge(IReadOnlyList<SeriesRunMergeStep> merges,
        IReadOnlyDictionary<string, AgentSmith.Application.Services.Holds.TicketHold>? holds)
    {
        var ready = merges.FirstOrDefault(s => s.Wait == SeriesRunMergeWaits.Ready);
        if (ready is null) return null;
        return holds is null || holds.GetValueOrDefault(ready.TicketId)?.Case == AgentSmith.Application.Services.Holds.TicketHoldCases.MergeNext
            ? ready.Key : null;
    }

    private static SeriesRunTicketView View(SeriesTicket ticket, SeriesRunOrderTicket ordered, IReadOnlyDictionary<string, int> depths,
        SeriesRunProjectFacts facts, IReadOnlyList<SeriesRunPullRequestView> prs) =>
        new(ticket.TicketId, ticket.Key, ticket.Title, ticket.FollowUp, depths[ticket.TicketId], ordered.Requires,
            [.. prs.Select(p => p.Repo).Distinct(StringComparer.Ordinal)], facts.Landed.Contains(ticket.TicketId),
            facts.Attempts.GetValueOrDefault(ticket.TicketId)?.Status, prs)
        { RunId = facts.Attempts.GetValueOrDefault(ticket.TicketId)?.RunId }; // 2026-11-23-f562

    private static SeriesRunCounts Counts(IReadOnlyList<SeriesRunTicketView> tickets, SeriesRunProjectFacts facts)
    {
        var open = tickets.Where(t => !t.Landed).ToList();
        var waiting = open.Where(t => t.PullRequests.Any(SeriesRunPullRequests.Waits)).ToList();
        return new(tickets.Count, tickets.Count - open.Count, waiting.Count, tickets.Count(t => SeriesRunTicketView.Held(t.Hold)),
            open.Count(t => t.PullRequests.Count == 0 && !facts.Attempts.ContainsKey(t.TicketId)));
    }
}
