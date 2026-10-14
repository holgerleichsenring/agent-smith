namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: one entry of a pull request's record — a comment, a review body or a decision
/// with no note of its own, one per host id. <see cref="Decision"/> is set on the entry the state
/// reader named; the current decision is only where <see cref="Standing"/> is true.
/// </summary>
/// <param name="Kind">One of <see cref="PrReviewEntryKinds"/>.</param>
/// <param name="IsSystem">A host-written note (an Azure DevOps vote comment, a GitLab system note).</param>
/// <param name="ThreadHostId">The thread the note belongs to; null for a decision row.</param>
/// <param name="Truncated">The body was cut at the stored cap.</param>
public sealed record PrReviewEntryView(
    string HostId,
    string Kind,
    string? Decision,
    bool Standing,
    string? AuthorLogin,
    bool Trusted,
    bool IsOurs,
    bool IsBot,
    bool IsSystem,
    string? File,
    int? Line,
    bool? Resolved,
    string? ThreadHostId,
    string Body,
    bool Truncated,
    DateTimeOffset At);
