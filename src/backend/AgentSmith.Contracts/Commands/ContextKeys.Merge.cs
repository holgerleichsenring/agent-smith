namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>
    /// 2026-11-17-023fa: the pull requests of a run that reached CommitAndPR's success tail — every spec verified
    /// green, code changed, no repo failed to open, none a draft. CompletePullRequests merges only these, so a red,
    /// partial or record-only run never reaches a merge.
    /// </summary>
    public const string GatePassedPullRequests = "GatePassedPullRequests";

    /// <summary>2026-11-17-023fb: a refresh-base run's pull request — its URL, its number and the base branch whose
    /// landing moved it — seeded as strings so the request survives the queue.</summary>
    public const string BaseRefreshPrUrl = "BaseRefreshPrUrl";

    public const string BaseRefreshPrNumber = "BaseRefreshPrNumber";

    public const string BaseRefreshBaseBranch = "BaseRefreshBaseBranch";

    /// <summary>2026-11-17-3435b: a merge-green run's <see cref="Runs.CiGreenMergeRequest"/> — the pull requests whose CI
    /// went green, seeded in process by the server's CI watch.</summary>
    public const string CiGreenMerge = "CiGreenMerge";
}
