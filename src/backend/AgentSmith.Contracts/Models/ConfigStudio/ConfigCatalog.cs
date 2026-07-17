namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// The canonical, editable configuration model behind <c>IConfigStore</c>.
/// Storage-agnostic: FileConfigStore materializes it from agentsmith.yml,
/// DbConfigStore from the relational catalog. Every studio read/write operates
/// on this shape; the entity lists mirror the dashboard's catalog tabs.
/// p0345b adds Connections (the p0281a git-host connections catalog) so an
/// operator config built on discovery refs renders fully in the studio.
/// </summary>
public sealed record ConfigCatalog(
    IReadOnlyList<AgentEntity> Agents,
    IReadOnlyList<TrackerEntity> Trackers,
    IReadOnlyList<RepoEntity> Repos,
    IReadOnlyList<ProjectEntity> Projects,
    IReadOnlyList<McpServerEntity> McpServers,
    IReadOnlyList<SecretEntity> Secrets,
    IReadOnlyList<ConnectionEntity> Connections)
{
    public ConfigCatalog() : this([], [], [], [], [], [], []) { }
}
