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
    Connection
}
