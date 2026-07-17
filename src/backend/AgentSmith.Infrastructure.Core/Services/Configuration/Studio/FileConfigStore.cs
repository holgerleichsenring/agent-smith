using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// File-backed <see cref="IConfigStore"/>: the canonical model lives as a
/// full-fidelity <see cref="RawAgentSmithConfig"/> read from agentsmith.yml, and
/// every mutation patches that document, records an attributed audit row, then
/// writes the file back. Because the document is preserved in full, an export
/// round-trips through the real <c>YamlConfigurationLoader</c> untouched — the
/// CLI/pipelines keep running purely file-based with zero DB dependency, exactly
/// as before this store existed. Deserialization mirrors the loader's converters
/// but does NOT resolve secrets, so the studio only ever holds env-NAMES.
/// </summary>
public sealed class FileConfigStore : IConfigStore
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new RawRepoRefYamlConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly IConfigStoreLocation _location;
    private readonly IConfigAuditStore _audit;
    private readonly ILogger<FileConfigStore> _logger;
    private readonly object _gate = new();

    private RawAgentSmithConfig? _document;
    private ConfigCatalog _catalog = new();

    public FileConfigStore(
        IConfigStoreLocation location,
        IConfigAuditStore audit,
        ILogger<FileConfigStore> logger)
    {
        _location = location;
        _audit = audit;
        _logger = logger;
    }

    public ConfigCatalog Catalog
    {
        get { lock (_gate) { EnsureLoaded(); return _catalog; } }
    }

    public ConfigCatalog Load()
    {
        lock (_gate)
        {
            _document = ReadDocument(_location.ConfigPath);
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

    // ---- agents -------------------------------------------------------------

    public IReadOnlyList<AgentEntity> GetAgents() => Catalog.Agents;

    public void UpsertAgent(AgentEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Agents, entity.Id);
        _document!.Agents[entity.Id] = BuildRawAgent(entity, _document.Agents.GetValueOrDefault(entity.Id));
        Record(by, ConfigEntityType.Agent, entity.Id, before, entity);
    });

    public void DeleteAgent(string id, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Agents, id);
        if (before is null) return;
        _document!.Agents.Remove(id);
        Record(by, ConfigEntityType.Agent, id, before, null);
    });

    // ---- trackers -----------------------------------------------------------

    public IReadOnlyList<TrackerEntity> GetTrackers() => Catalog.Trackers;

    public void UpsertTracker(TrackerEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Trackers, entity.Id);
        _document!.Trackers[entity.Id] = BuildRawTracker(entity, _document.Trackers.GetValueOrDefault(entity.Id));
        Record(by, ConfigEntityType.Tracker, entity.Id, before, entity);
    });

    public void DeleteTracker(string id, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Trackers, id);
        if (before is null) return;
        _document!.Trackers.Remove(id);
        Record(by, ConfigEntityType.Tracker, id, before, null);
    });

    // ---- repos --------------------------------------------------------------

    public IReadOnlyList<RepoEntity> GetRepos() => Catalog.Repos;

    public void UpsertRepo(RepoEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Repos, entity.Id);
        _document!.Repos[entity.Id] = BuildRawRepo(entity, _document.Repos.GetValueOrDefault(entity.Id));
        Record(by, ConfigEntityType.Repo, entity.Id, before, entity);
    });

    public void DeleteRepo(string id, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Repos, id);
        if (before is null) return;
        _document!.Repos.Remove(id);
        Record(by, ConfigEntityType.Repo, id, before, null);
    });

    // ---- projects (ref-validated) ------------------------------------------

    public IReadOnlyList<ProjectEntity> GetProjects() => Catalog.Projects;

    public void UpsertProject(ProjectEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        // Referential integrity: reject unknown agent/tracker/repo refs BEFORE
        // anything is persisted — the same guarantee the DB FKs and UI pickers give.
        ConfigReferentialValidator.ValidateProject(entity, _catalog);
        var before = Find(_catalog.Projects, entity.Id);
        _document!.Projects[entity.Id] = BuildRawProject(entity, _document.Projects.GetValueOrDefault(entity.Id));
        Record(by, ConfigEntityType.Project, entity.Id, before, entity);
    });

    public void DeleteProject(string id, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.Projects, id);
        if (before is null) return;
        _document!.Projects.Remove(id);
        Record(by, ConfigEntityType.Project, id, before, null);
    });

    // ---- mcp servers --------------------------------------------------------

    public IReadOnlyList<McpServerEntity> GetMcpServers() => Catalog.McpServers;

    public void UpsertMcpServer(McpServerEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.McpServers, entity.Id);
        _document!.McpServers[entity.Id] = BuildRawMcp(entity, _document.McpServers.GetValueOrDefault(entity.Id));
        Record(by, ConfigEntityType.McpServer, entity.Id, before, entity);
    });

    public void DeleteMcpServer(string id, ChangeAttribution by) => Mutate(() =>
    {
        var before = Find(_catalog.McpServers, id);
        if (before is null) return;
        _document!.McpServers.Remove(id);
        Record(by, ConfigEntityType.McpServer, id, before, null);
    });

    // ---- secrets (env-name only) -------------------------------------------

    public IReadOnlyList<SecretEntity> GetSecrets() => Catalog.Secrets;

    public void UpsertSecret(SecretEntity entity, ChangeAttribution by) => Mutate(() =>
    {
        var before = _catalog.Secrets.FirstOrDefault(s => s.Id == entity.Id);
        // Store the env-NAME reference only — never a value. Preserve an existing
        // placeholder; a brand-new secret references an env var of the same name.
        var existing = _document!.Secrets.GetValueOrDefault(entity.Id);
        _document.Secrets[entity.Id] = string.IsNullOrEmpty(existing) ? $"${{{entity.Id}}}" : existing;
        Record(by, ConfigEntityType.Secret, entity.Id, before, entity);
    });

    public void DeleteSecret(string id, ChangeAttribution by) => Mutate(() =>
    {
        var before = _catalog.Secrets.FirstOrDefault(s => s.Id == id);
        if (before is null) return;
        _document!.Secrets.Remove(id);
        Record(by, ConfigEntityType.Secret, id, before, null);
    });

    // ---- audit + revert -----------------------------------------------------

    public IReadOnlyList<ConfigChange> GetChanges() => _audit.GetAll();

    public void Revert(string changeId, ChangeAttribution by)
    {
        var change = _audit.GetById(changeId)
            ?? throw new ConfigurationException($"Unknown config change '{changeId}'.");

        switch (change.Operation)
        {
            case ConfigChangeOperation.Create:
                DeleteByType(change.EntityType, change.EntityId, by);
                break;
            case ConfigChangeOperation.Update:
            case ConfigChangeOperation.Delete:
                RestoreByType(change.EntityType, change.BeforeJson, by);
                break;
        }

        _audit.MarkReverted(changeId);
    }

    private void DeleteByType(ConfigEntityType type, string id, ChangeAttribution by)
    {
        switch (type)
        {
            case ConfigEntityType.Agent: DeleteAgent(id, by); break;
            case ConfigEntityType.Tracker: DeleteTracker(id, by); break;
            case ConfigEntityType.Repo: DeleteRepo(id, by); break;
            case ConfigEntityType.Project: DeleteProject(id, by); break;
            case ConfigEntityType.McpServer: DeleteMcpServer(id, by); break;
            case ConfigEntityType.Secret: DeleteSecret(id, by); break;
        }
    }

    private void RestoreByType(ConfigEntityType type, string? beforeJson, ChangeAttribution by)
    {
        if (beforeJson is null) return;
        switch (type)
        {
            case ConfigEntityType.Agent: UpsertAgent(Deserialize<AgentEntity>(beforeJson), by); break;
            case ConfigEntityType.Tracker: UpsertTracker(Deserialize<TrackerEntity>(beforeJson), by); break;
            case ConfigEntityType.Repo: UpsertRepo(Deserialize<RepoEntity>(beforeJson), by); break;
            case ConfigEntityType.Project: UpsertProject(Deserialize<ProjectEntity>(beforeJson), by); break;
            case ConfigEntityType.McpServer: UpsertMcpServer(Deserialize<McpServerEntity>(beforeJson), by); break;
            case ConfigEntityType.Secret: UpsertSecret(Deserialize<SecretEntity>(beforeJson), by); break;
        }
    }

    // ---- internals ----------------------------------------------------------

    private void Mutate(Action mutation)
    {
        lock (_gate)
        {
            EnsureLoaded();
            mutation();
            _catalog = ConfigCatalogMapper.ToCatalog(_document!);
            Save();
        }
    }

    private void Record(ChangeAttribution by, ConfigEntityType type, string id, object? before, object? after)
    {
        var op = before is null ? ConfigChangeOperation.Create
            : after is null ? ConfigChangeOperation.Delete
            : ConfigChangeOperation.Update;
        _audit.Append(
            by.Actor, type, id, op,
            before is null ? null : JsonSerializer.Serialize(before, Json),
            after is null ? null : JsonSerializer.Serialize(after, Json));
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_location.ConfigPath, ConfigYamlExporter.Export(_document!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist config to {Path}", _location.ConfigPath);
            throw;
        }
    }

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
            return Deserializer.Deserialize<RawAgentSmithConfig>(yaml) ?? new RawAgentSmithConfig();
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            throw new ConfigurationException($"Invalid YAML in {path}: {ex.Message}");
        }
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;

    private static T? Find<T>(IReadOnlyList<T> list, string id) where T : class =>
        list.FirstOrDefault(e => IdOf(e) == id);

    private static string IdOf(object entity) => entity switch
    {
        AgentEntity a => a.Id,
        TrackerEntity t => t.Id,
        RepoEntity r => r.Id,
        ProjectEntity p => p.Id,
        McpServerEntity m => m.Id,
        SecretEntity s => s.Id,
        _ => string.Empty
    };

    // ---- catalog -> raw patch builders --------------------------------------

    private static AgentConfig BuildRawAgent(AgentEntity entity, AgentConfig? existing)
    {
        var agent = existing ?? new AgentConfig();
        agent.Type = entity.Provider;
        agent.Model = entity.Models.TryGetValue("coding", out var coding) && !string.IsNullOrWhiteSpace(coding)
            ? coding
            : entity.Models.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? agent.Model;
        agent.ApiKeySecret = entity.KeySecret;
        return agent;
    }

    private static RawTrackerEntry BuildRawTracker(TrackerEntity entity, RawTrackerEntry? existing)
    {
        var tracker = existing ?? new RawTrackerEntry();
        tracker.Type = ParseEnum(entity.Type, TrackerType.GitHub);
        tracker.Organization = entity.Org;
        tracker.Project = entity.Project;
        tracker.Auth = entity.AuthSecret ?? string.Empty;
        return tracker;
    }

    private static RawRepoEntry BuildRawRepo(RepoEntity entity, RawRepoEntry? existing)
    {
        var repo = existing ?? new RawRepoEntry();
        if (entity.Name.Contains("://"))
        {
            repo.Url = entity.Name;
            if (existing is null) repo.Type = InferRepoType(entity.Name);
        }
        else
        {
            repo.Path = entity.Name;
            if (existing is null) repo.Type = RepoType.Local;
        }
        repo.DefaultBranch = entity.Branch;
        return repo;
    }

    private static RawProjectEntry BuildRawProject(ProjectEntity entity, RawProjectEntry? existing)
    {
        var project = existing ?? new RawProjectEntry();
        project.Agent = entity.Agent;
        project.Tracker = entity.Tracker;
        project.Repos = entity.Repos.Select(r => new RawRepoRef(r)).ToList();
        if (!string.IsNullOrWhiteSpace(entity.Trigger)) project.Pipeline = entity.Trigger!;
        if (entity.Pipelines.Count > 0)
            project.Pipelines = entity.Pipelines
                .Select(name => existing?.Pipelines.FirstOrDefault(p => p.Name == name) ?? new RawPipelineEntry { Name = name })
                .ToList();
        return project;
    }

    private static RawMcpServerEntry BuildRawMcp(McpServerEntity entity, RawMcpServerEntry? existing)
    {
        var mcp = existing ?? new RawMcpServerEntry();
        mcp.Transport = entity.Transport;
        mcp.Url = entity.Url;
        mcp.Auth = entity.AuthSecret;
        return mcp;
    }

    private static RepoType InferRepoType(string url) =>
        url.Contains("github", StringComparison.OrdinalIgnoreCase) ? RepoType.GitHub
        : url.Contains("gitlab", StringComparison.OrdinalIgnoreCase) ? RepoType.GitLab
        : url.Contains("dev.azure", StringComparison.OrdinalIgnoreCase)
            || url.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase) ? RepoType.AzureDevOps
        : RepoType.GitHub;

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum
    {
        var normalized = value.Replace("_", string.Empty);
        return Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
