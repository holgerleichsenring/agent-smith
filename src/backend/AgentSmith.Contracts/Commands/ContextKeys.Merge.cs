namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>
    /// 2026-11-17-023fa: the pull requests of a run that reached CommitAndPR's success tail — every spec verified
    /// green, code changed, no repo failed to open, none a draft. CompletePullRequests merges only these, so a red,
    /// partial or record-only run never reaches a merge.
    /// </summary>
    public const string GatePassedPullRequests = "GatePassedPullRequests";
}
