namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-17-3435b: the host needed no approval, got one, or refused it.</summary>
public enum PullRequestApprovalOutcome
{
    NotRequired = 0,
    Approved = 1,
    Refused = 2,
}
