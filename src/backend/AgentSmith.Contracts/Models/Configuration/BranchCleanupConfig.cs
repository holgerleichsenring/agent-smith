namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-713ec: how often the server starts a branch-cleanup run per project, from agentsmith.yml's top-level
/// <c>branch_cleanup:</c> block. A person's merge on GitHub or Azure Repos, and every closed pull request, leaves
/// its agent-smith/ branch behind, and nothing reports either, so the interval is what notices them.
/// </summary>
public sealed class BranchCleanupConfig
{
    /// <summary>The fewest seconds between two runs of a project.</summary>
    public const int MinIntervalSeconds = 3600;

    /// <summary>Whether the server starts branch-cleanup runs at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The seconds between two branch-cleanup runs of a project.</summary>
    public int IntervalSeconds { get; set; } = 86400;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule — the load and
    /// the settings store refuse what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems() =>
        IntervalSeconds >= MinIntervalSeconds
            ? []
            : [$"branch_cleanup.interval_seconds is {IntervalSeconds}; it must be {MinIntervalSeconds} or more, because "
               + "each run lists every agent-smith/ branch and its pull requests over the host API, which rate-limits"];
}
