namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9d: one review thread on a pull request, anchored at File/Line when it is an inline
/// thread. Resolved is null when the host offers no resolution for it (a top-level comment, a review
/// body), so it counts only while it is new.
/// </summary>
public sealed record PrReviewThread(string? File, int? Line, bool? Resolved, IReadOnlyList<PrReviewNote> Notes)
{
    /// <summary>2026-10-13-9a59b: the host's id of the thread — a GitHub review-thread node, a GitLab
    /// discussion, an Azure DevOps thread; a single-note thread carries its note's id.</summary>
    public string HostId { get; init; } = string.Empty;

    /// <summary>2026-10-13-9a59b: the thread is a review's body (GitHub), not a comment.</summary>
    public bool FromReview { get; init; }
}
