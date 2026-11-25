namespace AgentSmith.Contracts.Commands;

public static partial class CommandNames
{
    /// <summary>2026-11-17-023fa: the merge step, after PrCrossLink — 2026-11-25-9992i: a code run's marks its gate-passed pull
    /// requests awaiting CI under review: none, a merge-green run's merges. Never a finalizer: a red run never reaches it.</summary>
    public const string CompletePullRequests = "CompletePullRequestsCommand";

    /// <summary>2026-11-17-023fb: merges a landed base into an open agent-smith pull request's branch and pushes it
    /// without force; a conflict is said on the pull request.</summary>
    public const string RefreshBranchBase = "RefreshBranchBaseCommand";
}
