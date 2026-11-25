using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-17-3435b: what a merge-green run merges — the ticket, the attempt whose gate passed and whose pull requests
/// waited for CI (its row records the merge too), the work branch, and each repository's pull request now green.
/// </summary>
public sealed record CiGreenMergeRequest(
    string TicketId, string AttemptRunId, string Branch, IReadOnlyList<CiGreenPullRequest> PullRequests)
{
    /// <summary>2026-11-23-31dce: the person who asked for this merge from the series page; null when the CI watch
    /// asked. A merge a person asked for runs under every review — the click is the person's merge.</summary>
    public string? AskedBy { get; init; }

    /// <summary>2026-11-24-f9c5c: the series branch these pull requests target; it merges past protection (a series branch
    /// carries no host policy) and the base guard accepts it. Null for pull requests into the default branch.</summary>
    public string? SeriesBranch { get; init; }

    /// <summary>2026-11-25-9992e: a merge somebody judged — a person from the series page, or (2026-11-25-96aeb) the series
    /// master with an <see cref="OverrideReason"/>: no unlanded prerequisite, rung or unreadable-base refusal holds it; the
    /// host's own rules still apply.</summary>
    public bool Judged { get; init; }

    /// <summary>2026-11-25-9992e: the protection this merge asks the host for, over the policy's; null keeps the policy's.</summary>
    public BranchProtection? Protection { get; init; }

    /// <summary>2026-11-25-96aeb: the series master's merge — its pull request's base is read from the host and one off the
    /// series and default branch is refused even when judged: merged there it would record a landing that is none.</summary>
    public bool ChecksTarget { get; init; }

    /// <summary>2026-11-25-96aeb: why the series master merges past a prerequisite or an unreadable base; the merge then is
    /// judged, and each repository's line names the refusal it went past. Null without one.</summary>
    public string? OverrideReason { get; init; }

    /// <summary>2026-11-25-96aeb: the ticket ids of the series that integrates on <see cref="SeriesBranch"/> — a prerequisite
    /// among them lands on the series branch, any other on the default branch. Empty without a series branch.</summary>
    public IReadOnlyCollection<string> SeriesMembers { get; init; } = [];
}
