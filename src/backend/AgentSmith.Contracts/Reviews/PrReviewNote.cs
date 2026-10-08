using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9d: one note of a pull-request review — a thread comment or a review body. Author
/// is null for a deleted account; IsBot and IsSystem are what the host says, never a name pattern.
/// </summary>
public sealed record PrReviewNote(
    PrCommentAuthor? Author, bool IsBot, bool IsSystem, DateTimeOffset At, string Body);
