using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9b: one ticket comment as the comment handlers read it — the envelope, the status
/// the payload carried, the body, the plan answers it holds, and the rework act it would be (null
/// when the payload carries no time of its own for the comment).
/// </summary>
public sealed record KeywordComment(
    IncomingTicketEnvelope Envelope,
    string PayloadStatus,
    string Body,
    Dictionary<string, string>? PlanAnswers,
    ReworkAct? Act)
{
    /// <summary>2026-11-25-9992g: the commenter as the repository's host names them, on an issue tracker that is a
    /// repository (GitHub, GitLab) — what a write-access check asks; null on Jira and Azure DevOps boards.</summary>
    public AgentSmith.Contracts.Webhooks.PrCommentAuthor? Writer { get; init; }
}
