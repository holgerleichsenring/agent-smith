namespace AgentSmith.Contracts.Runs;

/// <summary>2026-10-08-f114: where a rework act was made — a keyword comment on the ticket, or a
/// Request changes on the pull request.</summary>
public enum ReworkChannel
{
    Ticket,
    PullRequest,
    // 2026-10-08-2123: a person moved a finished ticket back into a trigger status.
    Status,
}
