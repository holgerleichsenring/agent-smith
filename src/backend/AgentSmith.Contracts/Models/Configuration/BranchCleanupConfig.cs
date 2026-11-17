namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-713ec: how often the server starts a branch-cleanup run per project, from agentsmith.yml's top-level
/// <c>branch_cleanup:</c> block. A person's merge on GitHub or Azure Repos, and every closed pull request, leaves
/// its agent-smith/ branch behind, and nothing reports either, so the interval is what notices them.
/// </summary>
public sealed class BranchCleanupConfig
{
    /// <summary>Whether the server starts branch-cleanup runs at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The seconds between two branch-cleanup runs of a project.</summary>
    public int IntervalSeconds { get; set; } = 86400;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule — the load and
    /// the settings store refuse what this names, never clamping it. 2026-11-17-713ec: no floor beyond a positive
    /// interval — how often to spend host API calls on the sweep is the operator's choice, and the interval is the
    /// configuration that states it.</summary>
    public IReadOnlyList<string> Problems() =>
        IntervalSeconds > 0
            ? []
            : [$"branch_cleanup.interval_seconds is {IntervalSeconds}; it must be a positive number of seconds, because "
               + "it is the wait between two branch-cleanup runs of a project and a wait of none or less is no interval"];
}
