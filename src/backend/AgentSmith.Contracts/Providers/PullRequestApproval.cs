namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-17-3435b: what an approval came to; a refusal carries the host's reason.</summary>
public sealed record PullRequestApproval(PullRequestApprovalOutcome Outcome, string? Reason = null)
{
    public static PullRequestApproval NotRequired => new(PullRequestApprovalOutcome.NotRequired);

    public static PullRequestApproval Approved => new(PullRequestApprovalOutcome.Approved);

    public static PullRequestApproval Refused(string reason) => new(PullRequestApprovalOutcome.Refused, reason);
}
