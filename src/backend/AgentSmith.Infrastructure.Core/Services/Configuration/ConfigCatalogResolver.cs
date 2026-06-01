using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts a <see cref="RawAgentSmithConfig"/> (the YAML-bound shape) into
/// the public <see cref="AgentSmithConfig"/> with all catalog references
/// materialized. Fails fast on unresolved references with an aggregated
/// <see cref="ConfigurationException"/> listing every issue.
/// </summary>
public sealed class ConfigCatalogResolver
{
    private readonly RepoCatalogBuilder _repos = new();
    private readonly TrackerCatalogBuilder _trackers = new();
    private readonly ResolvedProjectBuilder _projects = new();

    public AgentSmithConfig Resolve(RawAgentSmithConfig raw)
    {
        var errors = new List<string>();
        var repos = _repos.Build(raw.Repos, errors);
        var trackers = _trackers.Build(raw.Trackers, errors);
        ThrowIfErrors(errors);

        var projects = ResolveProjects(raw, repos, trackers, errors);
        ThrowIfErrors(errors);

        var registries = BuildRegistries(raw);
        return Compose(raw, repos, trackers, projects, registries);
    }

    private static IReadOnlyList<RegistryConfig> BuildRegistries(RawAgentSmithConfig raw)
    {
        if (raw.Registries.Count == 0) return Array.Empty<RegistryConfig>();
        var resolved = new List<RegistryConfig>(raw.Registries.Count);
        foreach (var entry in raw.Registries)
            resolved.Add(new RegistryConfig(entry.Host, entry.Username, entry.Token));
        return resolved;
    }

    private Dictionary<string, ResolvedProject> ResolveProjects(
        RawAgentSmithConfig raw,
        Dictionary<string, RepoConnection> repos,
        Dictionary<string, TrackerConnection> trackers,
        List<string> errors)
    {
        var result = new Dictionary<string, ResolvedProject>(raw.Projects.Count);
        foreach (var (name, entry) in raw.Projects)
        {
            var resolved = _projects.TryBuild(name, entry, raw.Agents, trackers, repos, errors);
            if (resolved is not null) result[name] = resolved;
        }
        return result;
    }

    private static AgentSmithConfig Compose(
        RawAgentSmithConfig raw,
        Dictionary<string, RepoConnection> repos,
        Dictionary<string, TrackerConnection> trackers,
        Dictionary<string, ResolvedProject> projects,
        IReadOnlyList<RegistryConfig> registries) =>
        new()
        {
            Agents = raw.Agents,
            Repos = repos,
            Trackers = trackers,
            PipelineTriggers = new PipelineTriggerMap(raw.PipelineTriggers),
            Projects = projects,
            Secrets = raw.Secrets,
            Registries = registries,
            Queue = raw.Queue,
            Skills = raw.Skills,
            PrimaryProvider = raw.PrimaryProvider,
            Limits = raw.Limits,
            PipelineStorage = raw.PipelineStorage,
            PipelineDataFlow = raw.PipelineDataFlow,
            Sandbox = raw.Sandbox,
            Orchestrator = raw.Orchestrator,
            PipelineCostCap = raw.PipelineCostCap,
        };

    private static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count == 0) return;
        var joined = string.Join(Environment.NewLine + "  - ", errors);
        throw new ConfigurationException(
            $"Configuration error(s):{Environment.NewLine}  - {joined}");
    }
}
