using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Builds one <see cref="ResolvedProject"/> from a <see cref="RawProjectEntry"/>
/// plus the already-built agents/repos/trackers catalogs. Unresolved name
/// references go into the errors list with precise messages.
/// </summary>
public sealed class ResolvedProjectBuilder(IConnectionRepoUrlBuilder urlBuilder)
{
    public ResolvedProjectBuilder() : this(new ConnectionRepoUrlBuilder()) { }

    public ResolvedProject? TryBuild(
        string name,
        RawProjectEntry raw,
        Dictionary<string, AgentConfig> agents,
        Dictionary<string, TrackerConnection> trackers,
        Dictionary<string, RepoConnection> repos,
        Dictionary<string, ResolvedConnection> connections,
        RepoGlobExpander? globExpander,
        List<string> errors)
    {
        var agent = ResolveAgent(name, raw.Agent, agents, errors);
        var tracker = ResolveTracker(name, raw.Tracker, trackers, errors);
        var repoList = ResolveRepos(name, raw.Repos, repos, connections, globExpander, errors);
        var pipelines = ResolvePipelines(name, raw.Pipelines, agents, errors);

        if (agent is null || tracker is null || repoList is null || pipelines is null) return null;

        return CreateProject(name, raw, agent, tracker, repoList, pipelines);
    }

    private static IReadOnlyList<PipelineDefinition>? ResolvePipelines(
        string project, IReadOnlyList<RawPipelineEntry> raws,
        IReadOnlyDictionary<string, AgentConfig> agents, List<string> errors)
    {
        var result = new List<PipelineDefinition>(raws.Count);
        var anyError = false;
        foreach (var r in raws)
        {
            if (string.IsNullOrEmpty(r.Name))
            {
                errors.Add($"Project '{project}': pipelines entry is missing required field 'name'.");
                anyError = true;
                continue;
            }

            AgentConfig? resolvedAgent = null;
            if (!string.IsNullOrEmpty(r.Agent))
            {
                if (!agents.TryGetValue(r.Agent, out resolvedAgent))
                {
                    errors.Add(
                        $"Project '{project}': pipeline '{r.Name}' references agent '{r.Agent}' " +
                        $"which is not defined in agents: catalog.");
                    anyError = true;
                }
            }

            result.Add(new PipelineDefinition
            {
                Name = r.Name,
                AgentName = string.IsNullOrEmpty(r.Agent) ? null : r.Agent,
                Agent = resolvedAgent,
                SkillsPath = r.SkillsPath,
                CodingPrinciplesPath = r.CodingPrinciplesPath,
            });
        }
        return anyError ? null : result;
    }

    private static AgentConfig? ResolveAgent(
        string project, string agentName,
        IReadOnlyDictionary<string, AgentConfig> agents, List<string> errors)
    {
        if (string.IsNullOrEmpty(agentName))
        {
            errors.Add($"Project '{project}': missing required reference 'agent'.");
            return null;
        }
        if (agents.TryGetValue(agentName, out var agent)) return agent;

        errors.Add($"Project '{project}': references agent '{agentName}' which is not defined in agents: catalog.");
        return null;
    }

    private static TrackerConnection? ResolveTracker(
        string project, string trackerName,
        IReadOnlyDictionary<string, TrackerConnection> trackers, List<string> errors)
    {
        if (string.IsNullOrEmpty(trackerName))
        {
            errors.Add($"Project '{project}': missing required reference 'tracker'.");
            return null;
        }
        if (trackers.TryGetValue(trackerName, out var tracker)) return tracker;

        errors.Add($"Project '{project}': references tracker '{trackerName}' which is not defined in trackers: catalog.");
        return null;
    }

    private IReadOnlyList<RepoConnection>? ResolveRepos(
        string project, IReadOnlyList<RawRepoRef> repoEntries,
        IReadOnlyDictionary<string, RepoConnection> repos,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        RepoGlobExpander? globExpander, List<string> errors)
    {
        if (repoEntries.Count == 0)
        {
            errors.Add($"Project '{project}': 'repos' must list at least one repo (catalog name or connection/glob).");
            return null;
        }

        // p0281a/p0285: an entry with a '/' is a connection reference. A wildcard-free include
        // (acme/Service.Api) resolves STATICALLY from the connection (no discovery); a wildcard
        // include or any exclude keeps the discovery path. A bare name is a legacy repos: catalog
        // entry. All three forms can coexist in one project.
        var connectionRefs = repoEntries.Where(e => RepoGlobRef.IsConnectionRef(e.Ref)).ToList();
        var legacyNames = repoEntries.Where(e => !RepoGlobRef.IsConnectionRef(e.Ref)).Select(e => e.Ref).ToList();

        var resolved = ResolveLegacyRepos(project, legacyNames, repos, errors);
        if (resolved is null) return null;

        var exact = connectionRefs.Where(e => IsExactRef(e.Ref)).ToList();
        var globEntries = connectionRefs.Where(e => !IsExactRef(e.Ref)).ToList();

        if (!ResolveExactRefs(project, exact, connections, resolved, errors)) return null;
        if (!ResolveGlobRefs(project, globEntries, connections, globExpander, resolved, errors)) return null;

        return resolved;
    }

    private static bool IsExactRef(string entry)
    {
        var parsed = RepoGlobRef.Parse(entry);
        return !parsed.IsExclude && !parsed.IsGlob;
    }

    private bool ResolveExactRefs(
        string project, IReadOnlyList<RawRepoRef> exact,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        List<RepoConnection> resolved, List<string> errors)
    {
        var anyError = false;
        foreach (var entry in exact)
        {
            var parsed = RepoGlobRef.Parse(entry.Ref);
            if (!connections.TryGetValue(parsed.Connection, out var connection))
            {
                errors.Add(
                    $"Project '{project}': repo reference uses connection '{parsed.Connection}' which is not " +
                    "defined in connections: catalog.");
                anyError = true;
                continue;
            }
            resolved.Add(urlBuilder.Build(connection, parsed.Pattern, entry.DefaultBranch));
        }
        return !anyError;
    }

    private static bool ResolveGlobRefs(
        string project, IReadOnlyList<RawRepoRef> globEntries,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        RepoGlobExpander? globExpander, List<RepoConnection> resolved, List<string> errors)
    {
        if (globEntries.Count == 0) return true;
        if (globExpander is null)
        {
            errors.Add(
                $"Project '{project}': connection/glob repo references require repo discovery, " +
                "which is not available in this context.");
            return false;
        }

        var globRefs = globEntries.Select(e => RepoGlobRef.Parse(e.Ref)).ToList();
        resolved.AddRange(globExpander.Expand(project, globRefs, connections));
        return true;
    }

    private static List<RepoConnection>? ResolveLegacyRepos(
        string project, IReadOnlyList<string> names,
        IReadOnlyDictionary<string, RepoConnection> repos, List<string> errors)
    {
        var resolved = new List<RepoConnection>(names.Count);
        var anyMissing = false;
        foreach (var n in names)
        {
            if (repos.TryGetValue(n, out var r)) { resolved.Add(r); continue; }
            errors.Add($"Project '{project}': references repo '{n}' which is not defined in repos: catalog.");
            anyMissing = true;
        }
        return anyMissing ? null : resolved;
    }

    private static ResolvedProject CreateProject(
        string name, RawProjectEntry raw,
        AgentConfig agent, TrackerConnection tracker, IReadOnlyList<RepoConnection> repos,
        IReadOnlyList<PipelineDefinition> pipelines) =>
        new()
        {
            Name = name,
            Agent = agent,
            Tracker = tracker,
            Repos = repos,
            Pipeline = raw.Pipeline,
            Pipelines = pipelines,
            DefaultPipeline = raw.DefaultPipeline,
            CodingPrinciplesPath = raw.CodingPrinciplesPath,
            SkillsPath = raw.SkillsPath,
            JiraTrigger = raw.JiraTrigger,
            GithubTrigger = raw.GithubTrigger,
            GitlabTrigger = raw.GitlabTrigger,
            AzuredevopsTrigger = raw.AzuredevopsTrigger,
            Polling = raw.Polling,
            Sandbox = raw.Sandbox,
            Orchestrator = raw.Orchestrator,
        };
}
