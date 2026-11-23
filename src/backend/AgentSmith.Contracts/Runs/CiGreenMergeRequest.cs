namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-17-3435b: what a merge-green run merges — the ticket, the attempt whose gate passed and whose pull requests
/// waited for CI (its row records the merge too), the work branch, and each repository's pull request now green.
/// </summary>
public sealed record CiGreenMergeRequest(
    string TicketId, string AttemptRunId, string Branch, IReadOnlyList<CiGreenPullRequest> PullRequests)
{
    /// <summary>2026-11-23-31dce: the person who asked for this merge from the series page; null when the CI watch
    /// asked. A merge a person asked for runs under merge: manual too — the click is the manual merge.</summary>
    public string? AskedBy { get; init; }
}
