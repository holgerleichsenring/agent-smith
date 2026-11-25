using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-25-9992e: what the series master does to a member ticket's pull requests — each through the path a person's act
/// takes, each answer the sentence of what happened. The series' final pull requests are the beat's own (SeriesFinalMerge).
/// </summary>
public interface ISeriesPullRequestActs
{
    /// <summary>The ticket's waiting pull requests through a merge-green run asked by the series master:
    /// <paramref name="how"/> Bypass merges now where the identity may, Respect arms the host's auto-merge. 2026-11-25-96aeb: checked —
    /// an unlanded prerequisite or an unreadable base refuses unless <paramref name="overrideReason"/> says why to merge anyway; a
    /// base off the series and default branch always refuses.</summary>
    Task<SeriesMergeOutcome> MergeAsync(
        SeriesRunRecord series, string ticketId, BranchProtection how, string? overrideReason, CancellationToken cancellationToken);

    /// <summary>A refresh-base run for each of the ticket's open pull requests.</summary>
    Task<string> RefreshAsync(SeriesRunRecord series, string ticketId, CancellationToken cancellationToken);

    /// <summary>A retarget run moving the ticket's open pull request in <paramref name="repo"/> onto <paramref name="target"/>.</summary>
    Task<string> RetargetAsync(SeriesRunRecord series, string ticketId, string repo, string target, CancellationToken cancellationToken);

    /// <summary><paramref name="act"/> read: the records read from the host now, then each open pull request's CI on its head;
    /// requeue: its not-started builds queued again; start: its pull-request CI started.</summary>
    Task<string> CiAsync(SeriesRunRecord series, string ticketId, string act, CancellationToken cancellationToken);
}
