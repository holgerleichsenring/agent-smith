using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Storage-agnostic port over the canonical, editable configuration catalog.
/// FileConfigStore keeps the CLI/pipelines running purely from agentsmith.yml
/// with zero DB dependency; DbConfigStore is the server/UI adapter. Every
/// mutation is attributed and recorded as a revertible audit row, and any
/// project reference that is not present in the catalog is rejected at
/// validation (a <c>ConfigurationException</c>), never persisted.
/// </summary>
public interface IConfigStore
{
    /// <summary>Load (or reload) the working catalog from the backing store.</summary>
    ConfigCatalog Load();

    /// <summary>The current in-memory catalog.</summary>
    ConfigCatalog Catalog { get; }

    /// <summary>Serialize the canonical model to an agentsmith.yml that round-trips through the real loader.</summary>
    string ExportYaml();

    IReadOnlyList<AgentEntity> GetAgents();
    void UpsertAgent(AgentEntity entity, ChangeAttribution by);
    void DeleteAgent(string id, ChangeAttribution by);

    IReadOnlyList<TrackerEntity> GetTrackers();
    void UpsertTracker(TrackerEntity entity, ChangeAttribution by);
    void DeleteTracker(string id, ChangeAttribution by);

    IReadOnlyList<RepoEntity> GetRepos();
    void UpsertRepo(RepoEntity entity, ChangeAttribution by);
    void DeleteRepo(string id, ChangeAttribution by);

    IReadOnlyList<ProjectEntity> GetProjects();
    void UpsertProject(ProjectEntity entity, ChangeAttribution by);
    void DeleteProject(string id, ChangeAttribution by);

    IReadOnlyList<McpServerEntity> GetMcpServers();
    void UpsertMcpServer(McpServerEntity entity, ChangeAttribution by);
    void DeleteMcpServer(string id, ChangeAttribution by);

    IReadOnlyList<SecretEntity> GetSecrets();
    void UpsertSecret(SecretEntity entity, ChangeAttribution by);
    void DeleteSecret(string id, ChangeAttribution by);

    // p0345b: git-host connections (the p0281a discovery catalog) — first-class
    // like the other kinds, so connection-scoped project refs validate against it.
    IReadOnlyList<ConnectionEntity> GetConnections();
    void UpsertConnection(ConnectionEntity entity, ChangeAttribution by);
    void DeleteConnection(string id, ChangeAttribution by);

    // p0353: the global SETTINGS singletons — the taxonomy's singleton config docs
    // (orchestrator, limits, cost cap, skills, sandbox, …) surfaced as editable typed
    // forms. A generic surface keyed by the settings type: read the assembled value,
    // save the typed doc through the SAME attributed/versioned path as an entity
    // upsert (so a settings change shows in Changes and is revertible). persistence is
    // excluded — it is bootstrap-only and never editable.

    /// <summary>The editable settings singleton types (every taxonomy singleton minus bootstrap-only persistence).</summary>
    IReadOnlyList<string> SettingTypes { get; }

    /// <summary>Read one settings singleton as its typed value (serialized camelCase on the wire).</summary>
    object GetSetting(string type);

    /// <summary>Persist one settings singleton doc, attributed + versioned like an entity upsert.</summary>
    void SaveSetting(string type, System.Text.Json.JsonElement doc, ChangeAttribution by);

    /// <summary>The attributed change feed, newest first, for the Changes view.</summary>
    IReadOnlyList<ConfigChange> GetChanges();

    /// <summary>Replay the inverse of a recorded change, itself recorded as a new attributed row.</summary>
    void Revert(string changeId, ChangeAttribution by);
}
