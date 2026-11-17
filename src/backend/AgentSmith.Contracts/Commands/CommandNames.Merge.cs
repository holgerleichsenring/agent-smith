namespace AgentSmith.Contracts.Commands;

public static partial class CommandNames
{
    /// <summary>2026-11-17-023fa: merges a green code run's pull requests when the project's merge policy is
    /// on_gate_green, after PrCrossLink. Never a finalizer: a red run never reaches it.</summary>
    public const string CompletePullRequests = "CompletePullRequestsCommand";

    /// <summary>2026-11-17-023fb: merges a landed base into an open agent-smith pull request's branch and pushes it
    /// without force; a conflict is said on the pull request.</summary>
    public const string RefreshBranchBase = "RefreshBranchBaseCommand";
}
