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
    ReworkAct? Act);
