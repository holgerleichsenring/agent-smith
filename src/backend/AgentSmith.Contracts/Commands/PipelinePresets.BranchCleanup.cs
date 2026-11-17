namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-17-713ec: the run that removes finished agent-smith/ branches. The server starts
    /// it per project on branch_cleanup.interval_seconds; a branch delete is a repository write, so it is a
    /// step of a run with a run id rather than a server service.</summary>
    public const string BranchCleanupName = "branch-cleanup";

    public static readonly IReadOnlyList<string> BranchCleanup =
    [
        CommandNames.LoadCatalog, // p0205: every preset binds the skill catalog first
        CommandNames.CleanupWorkBranches,
    ];
}
