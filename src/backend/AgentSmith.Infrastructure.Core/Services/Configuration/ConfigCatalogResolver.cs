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
///
/// p0515: every catalog built here is keyed by <see cref="ConfigNames.Comparer"/>, so a
/// configured name resolves the same however it is capitalised. The RAW dictionaries stay
/// ordinal on purpose — they are what makes a pair of keys differing only in case
/// detectable, which a collapsed raw side would have hidden before anyone could name it.
/// </summary>
public sealed class ConfigCatalogResolver(
    RepoGlobExpander? globExpander = null,
    IConnectionRepoUrlBuilder? urlBuilder = null,
    IStartupFindings? findings = null)
{
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();
    private readonly CatalogKeyCollisions _collisions = new();
    private readonly AgentCatalogBuilder _agents = new();
    private readonly RepoCatalogBuilder _repos = new();
    private readonly TrackerCatalogBuilder _trackers = new();
    private readonly ConnectionCatalogBuilder _connections = new();
    private readonly ResolvedConfigComposer _composer = new();
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
        var catalogs = new ConfigCatalogs(
            _agents.Build(raw.Agents, collected),
            _repos.Build(raw.Repos, collected),
            _trackers.Build(raw.Trackers, collected),
            _connections.Build(raw.Connections, collected));

        var projects = ResolveProjects(raw, catalogs, collected);
        Publish(collected);

        return _composer.Compose(raw, catalogs, projects);
    }

    private void Publish(List<StartupFinding> collected)
    {
        LastFindings = collected;
        foreach (var finding in collected) _findings.Record(finding);
    }

    private Dictionary<string, ResolvedProject> ResolveProjects(
        RawAgentSmithConfig raw, ConfigCatalogs catalogs, List<StartupFinding> findings)
    {
        var dropped = _collisions.Detect("projects", raw.Projects.Keys, findings);
        var result = new Dictionary<string, ResolvedProject>(raw.Projects.Count, ConfigNames.Comparer);
        foreach (var (name, entry) in raw.Projects)
        {
            if (dropped.Contains(name)) continue;
            var resolved = TryBuildProject(name, entry, catalogs, findings);
            if (resolved is not null) result[name] = resolved;
        }
        return result;
    }

    // Repo-glob expansion and static connection-URL building still signal by exception —
    // they are reached from deep inside the repo list and have no error list to write to.
    // Catching here keeps the blast radius at one project instead of the whole config,
    // which is what letting it escape used to cost.
    private ResolvedProject? TryBuildProject(
        string name, RawProjectEntry entry, ConfigCatalogs catalogs, List<StartupFinding> findings)
    {
        try
        {
            return _projects.TryBuild(name, entry, catalogs, globExpander, findings);
        }
        catch (ConfigurationException ex)
        {
            findings.Add(ProjectFindings.Blocking(name, "repos", ex.Message));
            return null;
        }
    }
}
