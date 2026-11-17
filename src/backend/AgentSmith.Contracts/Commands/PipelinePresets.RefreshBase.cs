namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-17-023fb: the run that merges a landed base into one open agent-smith pull request's branch.
    /// The server starts it per sibling after a merged landing, seeded with the repository, the branch and the base;
    /// a branch write is a repository write, so it is a run with a run id rather than a server service.</summary>
    public const string RefreshBaseName = "refresh-base";

    public static readonly IReadOnlyList<string> RefreshBase =
    [
        CommandNames.LoadCatalog, // p0205: every preset binds the skill catalog first
        CommandNames.CheckoutSource,
        CommandNames.RefreshBranchBase,
    ];
}
