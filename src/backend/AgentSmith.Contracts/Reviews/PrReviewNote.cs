using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9d: one note of a pull-request review — a thread comment or a review body. Author
/// is null for a deleted account; IsBot and IsSystem are what the host says, never a name pattern.
/// </summary>
public sealed record PrReviewNote(
    PrCommentAuthor? Author, bool IsBot, bool IsSystem, DateTimeOffset At, string Body)
{
    /// <summary>2026-10-08-f147: written by agent-smith — the agentsmith marker AND the token's own
    /// account. A marker alone is text anyone can paste.</summary>
    public bool IsOurs { get; init; }

    /// <summary>2026-10-13-9a59b: the host's own id of the note — a GitHub node id, a GitLab note id,
    /// an Azure DevOps "{threadId}/{commentId}". A decision's id is the id of the note the host
    /// returns for the same object, so a decision and its review body are one entry.</summary>
    public string HostId { get; init; } = string.Empty;

    /// <summary>2026-11-19-1b72e: the host's note of a reviewer's vote — cast, changed, reset or withdrawn
    /// (an Azure DevOps VoteUpdate thread, a GitLab approve/unapprove/requested-changes note). A system note
    /// that is not a vote is the host narrating the pull request.</summary>
    public bool IsVote { get; init; }
}
