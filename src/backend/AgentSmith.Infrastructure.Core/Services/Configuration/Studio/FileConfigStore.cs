using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: READ-ONLY file-backed <see cref="IConfigStore"/>. The canonical model is
/// a full-fidelity <see cref="RawAgentSmithConfig"/> read from agentsmith.yml and
/// projected onto the studio catalog; an export round-trips through the real
/// <c>YamlConfigurationLoader</c> untouched. This is the CLI's store — a one-shot
/// scan never edits config, so the file-writeback that p0345 shipped (a no-op
/// against a read-only ConfigMap anyway) is removed. Editing lives on the server's
/// DbConfigStore. Deserialization does NOT resolve secrets, so it only ever holds
/// env-NAMES.
/// </summary>
public sealed class FileConfigStore(IConfigStoreLocation location) : IConfigStore
{
    private readonly object _gate = new();
    private RawAgentSmithConfig? _document;
    private ConfigCatalog _catalog = new();

    public ConfigCatalog Catalog
    {
        get { lock (_gate) { EnsureLoaded(); return _catalog; } }
    }

    public ConfigCatalog Load()
    {
        lock (_gate)
        {
            _document = ReadDocument(location.ConfigPath);
            _catalog = ConfigCatalogMapper.ToCatalog(_document);
            return _catalog;
        }
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
    public IReadOnlyList<ConfigChange> GetChanges() => [];

    // p0353: the settings singletons READ off the same file-backed document; the CLI
    // store never writes back, so a save is the read-only error like every mutation.
    public IReadOnlyList<string> SettingTypes => ConfigSettingsAccess.Types;

    public object GetSetting(string type)
    {
        lock (_gate) { EnsureLoaded(); return ConfigSettingsAccess.Read(_document!, type); }
    }

    public void SaveSetting(string type, JsonElement doc, ChangeAttribution by) => throw ReadOnly();

    public void UpsertAgent(AgentEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteAgent(string id, ChangeAttribution by) => throw ReadOnly();
    public void UpsertTracker(TrackerEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteTracker(string id, ChangeAttribution by) => throw ReadOnly();
    public void UpsertRepo(RepoEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteRepo(string id, ChangeAttribution by) => throw ReadOnly();
    public void UpsertProject(ProjectEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteProject(string id, ChangeAttribution by) => throw ReadOnly();
    public void UpsertMcpServer(McpServerEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteMcpServer(string id, ChangeAttribution by) => throw ReadOnly();
    public void UpsertSecret(SecretEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteSecret(string id, ChangeAttribution by) => throw ReadOnly();
    public void UpsertConnection(ConnectionEntity entity, ChangeAttribution by) => throw ReadOnly();
    public void DeleteConnection(string id, ChangeAttribution by) => throw ReadOnly();
    public void Revert(string changeId, ChangeAttribution by) => throw ReadOnly();

    private static NotSupportedException ReadOnly() => new(
        "FileConfigStore is read-only. Edit config on the server's DbConfigStore " +
        "(config studio); the CLI reads agentsmith.yml and never writes it back.");

    private void EnsureLoaded()
    {
        if (_document is null) Load();
    }

    private static RawAgentSmithConfig ReadDocument(string path)
    {
        if (!File.Exists(path))
            throw new ConfigurationException($"Configuration file not found: {path}");
        var yaml = File.ReadAllText(path);
        try
        {
            return RawConfigYaml.Deserialize(yaml);
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            throw new ConfigurationException($"Invalid YAML in {path}: {ex.Message}");
        }
    }
}
