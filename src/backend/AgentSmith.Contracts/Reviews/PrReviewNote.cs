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
}
