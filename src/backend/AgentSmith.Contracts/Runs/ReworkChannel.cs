namespace AgentSmith.Contracts.Runs;

/// <summary>2026-10-08-f114: where a rework act was made — a keyword comment on the ticket, or a
/// Request changes on the pull request.</summary>
public enum ReworkChannel
{
    Ticket,
    PullRequest,
    // 2026-10-08-2123: a person moved a finished ticket back into a trigger status.
    Status,
    // 2026-10-13-9a59m: a person approved a change in the ticket's own conversation.
    Operator,
    // 2026-10-13-9a59n: a person asked for an unchanged attempt after an infrastructure end.
    Retry,
    // 2026-10-13-8762p: a person overruled a default on the ticket page.
    Overrule,
}
