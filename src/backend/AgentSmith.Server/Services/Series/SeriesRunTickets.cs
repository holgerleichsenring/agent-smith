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
/// tickets What holds the rest lists — none when no holds were read (the merge order alone needs none).</para>
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
        return new SeriesRunTicketsShape(tickets, Counts(tickets, facts), order.MergeOrder(ordered, facts.Landed));
    }

    private static SeriesRunTicketView View(SeriesTicket ticket, SeriesRunOrderTicket ordered, IReadOnlyDictionary<string, int> depths,
        SeriesRunProjectFacts facts, IReadOnlyList<SeriesRunPullRequestView> prs) =>
        new(ticket.TicketId, ticket.Key, ticket.Title, ticket.FollowUp, depths[ticket.TicketId], ordered.Requires,
            [.. prs.Select(p => p.Repo).Distinct(StringComparer.Ordinal)], facts.Landed.Contains(ticket.TicketId),
            facts.Attempts.GetValueOrDefault(ticket.TicketId)?.Status, prs);

    private static SeriesRunCounts Counts(IReadOnlyList<SeriesRunTicketView> tickets, SeriesRunProjectFacts facts)
    {
        var open = tickets.Where(t => !t.Landed).ToList();
        var waiting = open.Where(t => t.PullRequests.Any(SeriesRunPullRequests.Waits)).ToList();
        return new(tickets.Count, tickets.Count - open.Count, waiting.Count, tickets.Count(t => SeriesRunTicketView.Held(t.Hold)),
            open.Count(t => t.PullRequests.Count == 0 && !facts.Attempts.ContainsKey(t.TicketId)));
    }
}
