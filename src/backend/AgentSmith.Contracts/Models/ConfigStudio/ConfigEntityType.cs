namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>The catalog entity kinds the studio can create/edit.</summary>
public enum ConfigEntityType
{
    Agent,
    Tracker,
    Repo,
    Project,
    McpServer,
    Secret,
    // p0345b: git-host connections (the p0281a discovery catalog).
    Connection,
    // 2026-10-01-7f7aa: design tools a project reads designs from.
    DesignSource,
    // p0353: the global settings singletons (orchestrator, limits, pipeline_cost_cap, …),
    // addressed by their settings-type key so one kind covers every singleton form.
    Settings
}
