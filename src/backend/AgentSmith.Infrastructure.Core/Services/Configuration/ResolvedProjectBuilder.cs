using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Builds one <see cref="ResolvedProject"/> from a <see cref="RawProjectEntry"/>
/// plus the already-built agents/repos/trackers catalogs. Unresolved name
/// references go into the errors list with precise messages.
/// </summary>
public sealed class ResolvedProjectBuilder
{
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

    private static IReadOnlyList<RepoConnection>? ResolveRepos(
        string project, IReadOnlyList<string> repoEntries,
        IReadOnlyDictionary<string, RepoConnection> repos,
        IReadOnlyDictionary<string, ResolvedConnection> connections,
        RepoGlobExpander? globExpander, List<string> errors)
    {
        if (repoEntries.Count == 0)
        {
            errors.Add($"Project '{project}': 'repos' must list at least one repo (catalog name or connection/glob).");
            return null;
        }

        // p0281a: an entry with a '/' (optionally '!'-prefixed) is a connection/glob reference,
        // expanded against the connection's discovered repos; a bare name is a legacy repos:
        // catalog entry. Both forms can coexist in one project.
        var globRefs = repoEntries.Where(RepoGlobRef.IsConnectionRef).Select(RepoGlobRef.Parse).ToList();
        var legacyNames = repoEntries.Where(e => !RepoGlobRef.IsConnectionRef(e)).ToList();

        var resolved = ResolveLegacyRepos(project, legacyNames, repos, errors);
        if (resolved is null) return null;

        if (globRefs.Count > 0)
        {
            if (globExpander is null)
            {
                errors.Add(
                    $"Project '{project}': connection/glob repo references require repo discovery, " +
                    "which is not available in this context.");
                return null;
            }
            resolved.AddRange(globExpander.Expand(project, globRefs, connections));
        }

        return resolved;
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
