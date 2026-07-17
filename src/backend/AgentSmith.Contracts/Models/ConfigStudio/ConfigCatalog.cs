namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// The canonical, editable configuration model behind <c>IConfigStore</c>.
/// Storage-agnostic: FileConfigStore materializes it from agentsmith.yml,
/// DbConfigStore from the relational catalog. Every studio read/write operates
/// on this shape; the six entity lists mirror the dashboard's catalog tabs.
/// </summary>
public sealed record ConfigCatalog(
    IReadOnlyList<AgentEntity> Agents,
    IReadOnlyList<TrackerEntity> Trackers,
    IReadOnlyList<RepoEntity> Repos,
    IReadOnlyList<ProjectEntity> Projects,
    IReadOnlyList<McpServerEntity> McpServers,
    IReadOnlyList<SecretEntity> Secrets)
{
    public ConfigCatalog() : this([], [], [], [], [], []) { }
}
