using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: one reviewer's standing decision. <see cref="HostId"/> is the id of the note
/// the thread reader returns for the same object — a GitHub review, a GitLab system note, an
/// Azure DevOps vote-thread comment — or "vote:{reviewerId}" for an Azure DevOps vote with no
/// vote thread.
/// </summary>
/// <param name="Decision">One of <see cref="PrReviewDecisions"/>.</param>
public sealed record PrReviewDecision(
    string HostId, PrCommentAuthor? Author, bool IsBot, string Decision, DateTimeOffset At, string Body);
