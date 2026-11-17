namespace AgentSmith.Contracts.Commands;

public static partial class CommandNames
{
    /// <summary>2026-11-17-713ec: deletes the agent-smith/ branches whose pull request merged or
    /// closed and that have not moved since, in every repository of the run's project. The one step
    /// of the branch-cleanup preset.</summary>
    public const string CleanupWorkBranches = "CleanupWorkBranchesCommand";
}
