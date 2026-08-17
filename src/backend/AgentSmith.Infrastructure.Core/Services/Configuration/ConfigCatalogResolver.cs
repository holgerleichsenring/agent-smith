using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Converts a <see cref="RawAgentSmithConfig"/> (the YAML-bound shape) into
/// the public <see cref="AgentSmithConfig"/> with all catalog references
/// materialized.
///
/// p0391b: an unresolved reference is ONE <see cref="StartupFinding"/> naming its project
/// and its field, not one aggregated throw. Six mistakes give an operator six lines to fix
/// rather than one wall of text, and the project that carries them is the only unit that
/// drops out — every other project still materializes and still runs.
/// </summary>
public sealed class ConfigCatalogResolver(
    RepoGlobExpander? globExpander = null,
    IConnectionRepoUrlBuilder? urlBuilder = null,
    IStartupFindings? findings = null)
{
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();
    private readonly RepoCatalogBuilder _repos = new();
    private readonly TrackerCatalogBuilder _trackers = new();
    private readonly ConnectionCatalogBuilder _connections = new();
    private readonly ResolvedProjectBuilder _projects = new(urlBuilder ?? new ConnectionRepoUrlBuilder());

    /// <summary>
    /// What the LAST <see cref="Resolve"/> call could not materialize. The server reads
    /// these off the shared findings list; the one-shot file loader reads them here to
    /// tell its operator, on stderr, why the configuration they just passed is unusable.
    /// </summary>
    public IReadOnlyList<StartupFinding> LastFindings { get; private set; } = [];

    public AgentSmithConfig Resolve(RawAgentSmithConfig raw)
    {
        var collected = new List<StartupFinding>();
        var repos = _repos.Build(raw.Repos);
        var trackers = _trackers.Build(raw.Trackers);
        var connections = _connections.Build(raw.Connections);

        var projects = ResolveProjects(raw, repos, trackers, connections, collected);
        Publish(collected);

        var registries = BuildRegistries(raw);
        return Compose(raw, repos, trackers, connections, projects, registries);
    }

    private void Publish(List<StartupFinding> collected)
    {
        LastFindings = collected;
        foreach (var finding in collected) _findings.Record(finding);
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
        Dictionary<string, ResolvedConnection> connections,
        List<StartupFinding> findings)
    {
        var result = new Dictionary<string, ResolvedProject>(raw.Projects.Count);
        foreach (var (name, entry) in raw.Projects)
        {
            var resolved = TryBuildProject(name, entry, raw, repos, trackers, connections, findings);
            if (resolved is not null) result[name] = resolved;
        }
        return result;
    }

    // Repo-glob expansion and static connection-URL building still signal by exception —
    // they are reached from deep inside the repo list and have no error list to write to.
    // Catching here keeps the blast radius at one project instead of the whole config,
    // which is what letting it escape used to cost.
    private ResolvedProject? TryBuildProject(
        string name,
        RawProjectEntry entry,
        RawAgentSmithConfig raw,
        Dictionary<string, RepoConnection> repos,
        Dictionary<string, TrackerConnection> trackers,
        Dictionary<string, ResolvedConnection> connections,
        List<StartupFinding> findings)
    {
        try
        {
            return _projects.TryBuild(
                name, entry, raw.Agents, trackers, repos, connections, globExpander, findings);
        }
        catch (ConfigurationException ex)
        {
            findings.Add(ProjectFindings.Blocking(name, "repos", ex.Message));
            return null;
        }
    }

    private static AgentSmithConfig Compose(
        RawAgentSmithConfig raw,
        Dictionary<string, RepoConnection> repos,
        Dictionary<string, TrackerConnection> trackers,
        Dictionary<string, ResolvedConnection> connections,
        Dictionary<string, ResolvedProject> projects,
        IReadOnlyList<RegistryConfig> registries) =>
        new()
        {
            Agents = raw.Agents,
            Repos = repos,
            Connections = connections,
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
            Dialogue = raw.Dialogue, // p0327
            Persistence = raw.Persistence, Trace = raw.Trace, // p0423
            PipelineCostCap = raw.PipelineCostCap,
        };
}
