using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcb: a series run's tickets from its project's facts — each with its depth, repos, pull requests per repo
/// and newest status — its progress counts and its merge order (<see cref="SeriesRunOrder"/>). 2026-11-24-f9c5c: an open pull
/// request's target is read per head; the series' branch is a merge target like the default branch; the merge itself still
/// reads the host's base.
/// <para>2026-11-23-31dcg: each ticket carries its hold, read where the ticket list reads it. 2026-11-25-9992a: the counts,
/// kinds and words come from the tickets' one state (<see cref="SeriesStatusReader"/>), not from this shape.</para>
/// </summary>
public sealed class SeriesRunTickets(SeriesRunOrder order, SeriesRunPullRequests pullRequests)
{
    public SeriesRunTicketsShape Of(SeriesRunRecord record, SeriesRunProjectFacts facts,
        IReadOnlyDictionary<string, AgentSmith.Application.Services.Holds.TicketHold>? holds = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(facts);
        var prs = record.Tickets.Select(t => t.TicketId).Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => pullRequests.Of(facts.PullRequests[id], facts.Project, facts.RecordOnly), StringComparer.Ordinal);
        var ordered = record.Tickets.Select(t => new SeriesRunOrderTicket(t.TicketId, t.Key, facts.Requires.GetValueOrDefault(t.TicketId) ?? [])
        {
            HasOpenPullRequest = prs[t.TicketId].Any(SeriesRunPullRequestView.Waits),
            TargetsElsewhere = prs[t.TicketId].Any(p => SeriesRunPullRequestView.Waits(p)
                && pullRequests.TargetsElsewhere(p, facts.Project, record.IntegrationBranch)), // 2026-11-24-f9c5c
        }).ToList();
        var depths = order.Depths(ordered);
        var tickets = record.Tickets.Select((t, index) => (View: View(t, ordered[index], depths, facts, prs[t.TicketId])
                with { Hold = holds?.GetValueOrDefault(t.TicketId) }, index))
            .OrderBy(x => x.View.Depth).ThenBy(x => x.index).Select(x => x.View).ToList();
        var merges = Needed(order.MergeOrder(ordered, facts.Landed), tickets, holds);
        return new SeriesRunTicketsShape(tickets, merges) { NextMerge = NextMerge(merges, holds) };
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
        {
            RunId = facts.Attempts.GetValueOrDefault(ticket.TicketId)?.RunId, // 2026-11-23-f562
            LaneRepo = LaneOf(facts.SpecRepos.GetValueOrDefault(ticket.TicketId) ?? [], prs), // 2026-11-27-ecf2b
        };

    /// <summary>2026-11-27-ecf2b: the repository a ticket is drawn on — its spec's, the one whose pull request carries
    /// the code first; a ticket without specs on its pull requests'.</summary>
    internal static string? LaneOf(IReadOnlyList<string> specRepos, IReadOnlyList<SeriesRunPullRequestView> prs)
    {
        bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        bool Code(string repo) => prs.Any(p => Same(p.Repo, repo) && !p.RecordOnly);
        // The pull request's spelling of a spec repository, so the lane is the one its pull requests are drawn under.
        string Named(string repo) => prs.FirstOrDefault(p => Same(p.Repo, repo))?.Repo ?? repo;
        var spec = specRepos.FirstOrDefault(Code) ?? specRepos.FirstOrDefault();
        return spec is not null ? Named(spec) : prs.FirstOrDefault(p => !p.RecordOnly)?.Repo ?? prs.FirstOrDefault()?.Repo;
    }
}
