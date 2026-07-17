using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// SKELETON — the server/UI relational adapter over the config catalog. The
/// schema is real (<see cref="ConfigStudioSchema"/>) and referential integrity +
/// audit are modelled by construction there; the live query/command wiring lands
/// with p0346 (relational runtime), which also moves runtime state off Redis and
/// settles the CLI/compose/k8s deployment story. Until then the file-backed
/// <see cref="FileConfigStore"/> is the shipping store, and this class exists to
/// pin the port contract and the schema. Every operation throws rather than
/// silently faking a store.
/// </summary>
public sealed class DbConfigStore : IConfigStore
{
    public string SchemaDdl => ConfigStudioSchema.Ddl;

    public ConfigCatalog Catalog => throw NotWired();
    public ConfigCatalog Load() => throw NotWired();
    public string ExportYaml() => throw NotWired();

    public IReadOnlyList<AgentEntity> GetAgents() => throw NotWired();
    public void UpsertAgent(AgentEntity entity, ChangeAttribution by) => throw NotWired();
    public void DeleteAgent(string id, ChangeAttribution by) => throw NotWired();

    public IReadOnlyList<TrackerEntity> GetTrackers() => throw NotWired();
    public void UpsertTracker(TrackerEntity entity, ChangeAttribution by) => throw NotWired();
    public void DeleteTracker(string id, ChangeAttribution by) => throw NotWired();

    public IReadOnlyList<RepoEntity> GetRepos() => throw NotWired();
    public void UpsertRepo(RepoEntity entity, ChangeAttribution by) => throw NotWired();
    public void DeleteRepo(string id, ChangeAttribution by) => throw NotWired();

    public IReadOnlyList<ProjectEntity> GetProjects() => throw NotWired();
    public void UpsertProject(ProjectEntity entity, ChangeAttribution by) => throw NotWired();
    public void DeleteProject(string id, ChangeAttribution by) => throw NotWired();

    public IReadOnlyList<McpServerEntity> GetMcpServers() => throw NotWired();
    public void UpsertMcpServer(McpServerEntity entity, ChangeAttribution by) => throw NotWired();
    public void DeleteMcpServer(string id, ChangeAttribution by) => throw NotWired();

    public IReadOnlyList<SecretEntity> GetSecrets() => throw NotWired();
    public void UpsertSecret(SecretEntity entity, ChangeAttribution by) => throw NotWired();
    public void DeleteSecret(string id, ChangeAttribution by) => throw NotWired();

    public IReadOnlyList<ConfigChange> GetChanges() => throw NotWired();
    public void Revert(string changeId, ChangeAttribution by) => throw NotWired();

    private static NotSupportedException NotWired() => new(
        "DbConfigStore is a schema+skeleton in this build; FileConfigStore is the live store. " +
        "Relational wiring lands with p0346 (relational runtime).");
}
