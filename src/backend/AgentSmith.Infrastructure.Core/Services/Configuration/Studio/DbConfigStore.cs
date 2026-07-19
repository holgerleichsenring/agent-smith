using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: the server/UI store — config as DB entity-document rows. Reads assemble a
/// <see cref="RawAgentSmithConfig"/> from the doc rows via the type&lt;-&gt;model map and
/// project it onto the studio catalog; writes patch one entity's raw slice and
/// persist just that doc (+ its edges) transactionally through
/// <see cref="IConfigDocumentStore"/>, version-checked and secret-guarded. The
/// single audit is config_entity_version, surfaced here as the Changes feed.
/// </summary>
public sealed class DbConfigStore(IConfigDocumentStore docStore, ConfigDocumentAssembler assembler) : IConfigStore
{
    private readonly object _gate = new();
    private RawAgentSmithConfig? _document;
    private ConfigCatalog _catalog = new();
    private Dictionary<(string, string), int> _versions = new();

    public ConfigCatalog Catalog
    {
        get { lock (_gate) { EnsureLoaded(); return _catalog; } }
    }

    public ConfigCatalog Load()
    {
        lock (_gate) { Reload(); return _catalog; }
    }

    public string ExportYaml()
    {
        lock (_gate)
        {
            EnsureLoaded();
            ConfigReferentialValidator.ValidateCatalog(_catalog);
            return ConfigYamlExporter.Export(_document!);
        }
    }

    public IReadOnlyList<AgentEntity> GetAgents() => Catalog.Agents;
    public IReadOnlyList<TrackerEntity> GetTrackers() => Catalog.Trackers;
    public IReadOnlyList<RepoEntity> GetRepos() => Catalog.Repos;
    public IReadOnlyList<ProjectEntity> GetProjects() => Catalog.Projects;
    public IReadOnlyList<McpServerEntity> GetMcpServers() => Catalog.McpServers;
    public IReadOnlyList<SecretEntity> GetSecrets() => Catalog.Secrets;
    public IReadOnlyList<ConnectionEntity> GetConnections() => Catalog.Connections;

    public void UpsertAgent(AgentEntity entity, ChangeAttribution by) => Mutate(() =>
        Save(ConfigDocTypes.Agent, entity.Id, RawConfigPatch.Agent(entity, Existing(_document!.Agents, entity.Id)), by));

    public void UpsertTracker(TrackerEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        ConfigStudioCapabilities.ValidateTracker(entity);
        Save(ConfigDocTypes.Tracker, entity.Id, RawConfigPatch.Tracker(entity, Existing(_document!.Trackers, entity.Id)), by);
    });

    public void UpsertRepo(RepoEntity entity, ChangeAttribution by) => Mutate(() =>
        Save(ConfigDocTypes.Repo, entity.Id, RawConfigPatch.Repo(entity, Existing(_document!.Repos, entity.Id)), by));

    public void UpsertProject(ProjectEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        ConfigReferentialValidator.ValidateProject(entity, _catalog);
        ConfigStudioCapabilities.ValidateProjectResolution(entity);
        Save(ConfigDocTypes.Project, entity.Id, RawConfigPatch.Project(entity, Existing(_document!.Projects, entity.Id)), by);
    });

    public void UpsertMcpServer(McpServerEntity entity, ChangeAttribution by) => Mutate(() =>
        Save(ConfigDocTypes.McpServer, entity.Id, RawConfigPatch.Mcp(entity, Existing(_document!.McpServers, entity.Id)), by));

    public void UpsertConnection(ConnectionEntity entity, ChangeAttribution by) => Mutate(() =>
        Save(ConfigDocTypes.Connection, entity.Id, RawConfigPatch.Connection(entity, Existing(_document!.Connections, entity.Id)), by));

    public void UpsertSecret(SecretEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        var existing = _document!.Secrets.GetValueOrDefault(entity.Id);
        Save(ConfigDocTypes.Secret, entity.Id, string.IsNullOrEmpty(existing) ? $"${{{entity.Id}}}" : existing, by);
    });

    public void DeleteAgent(string id, ChangeAttribution by) => Delete(ConfigDocTypes.Agent, id, by);
    public void DeleteTracker(string id, ChangeAttribution by) => Delete(ConfigDocTypes.Tracker, id, by);
    public void DeleteRepo(string id, ChangeAttribution by) => Delete(ConfigDocTypes.Repo, id, by);
    public void DeleteProject(string id, ChangeAttribution by) => Delete(ConfigDocTypes.Project, id, by);
    public void DeleteMcpServer(string id, ChangeAttribution by) => Delete(ConfigDocTypes.McpServer, id, by);
    public void DeleteSecret(string id, ChangeAttribution by) => Delete(ConfigDocTypes.Secret, id, by);
    public void DeleteConnection(string id, ChangeAttribution by) => Delete(ConfigDocTypes.Connection, id, by);

    public IReadOnlyList<ConfigChange> GetChanges() => ConfigChangeProjection.From(docStore.GetVersions());

    public void Revert(string changeId, ChangeAttribution by) => Mutate(() =>
    {
        var target = docStore.GetVersion(long.Parse(changeId))
            ?? throw new ConfigurationException($"Unknown config change '{changeId}'.");
        var prior = docStore.PriorDoc(target.Type, target.EntityId, target.Version);
        if (prior is null)
        {
            docStore.Delete(target.Type, target.EntityId, by.Actor);
            return;
        }
        docStore.Save(new ConfigDocWrite(
            target.Type, target.EntityId, prior, ExpectedVersion: null,
            assembler.EdgesFor(target.Type, prior), by.Actor, "revert"));
    });

    private void Save(string type, string id, object rawEntry, ChangeAttribution by)
    {
        var doc = JsonSerializer.Serialize(rawEntry, ConfigDocJson.Options);
        var edges = assembler.EdgesFor(type, doc);
        var expected = _versions.TryGetValue((type, id), out var v) ? v : (int?)null;
        docStore.Save(new ConfigDocWrite(type, id, doc, expected, edges, by.Actor));
    }

    private void Delete(string type, string id, ChangeAttribution by) => Mutate(() =>
        docStore.Delete(type, id, by.Actor));

    private void Mutate(Action mutation)
    {
        lock (_gate)
        {
            EnsureLoaded();
            mutation();
            Reload();
        }
    }

    private void Reload()
    {
        var rows = docStore.LoadAll();
        _document = assembler.Assemble(rows);
        _catalog = ConfigCatalogMapper.ToCatalog(_document);
        _versions = rows.ToDictionary(r => (r.Type, r.Id), r => r.Version);
    }

    private void EnsureLoaded()
    {
        if (_document is null) Reload();
    }

    private static T? Existing<T>(IReadOnlyDictionary<string, T> map, string id) where T : class =>
        map.GetValueOrDefault(id);
}
