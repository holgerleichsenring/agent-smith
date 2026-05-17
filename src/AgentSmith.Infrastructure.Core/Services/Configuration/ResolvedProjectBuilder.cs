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
        List<string> errors)
    {
        var agent = ResolveAgent(name, raw.Agent, agents, errors);
        var tracker = ResolveTracker(name, raw.Tracker, trackers, errors);
        var repoList = ResolveRepos(name, raw.Repos, repos, errors);

        if (agent is null || tracker is null || repoList is null) return null;

        return CreateProject(name, raw, agent, tracker, repoList);
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
        string project, IReadOnlyList<string> repoNames,
        IReadOnlyDictionary<string, RepoConnection> repos, List<string> errors)
    {
        if (repoNames.Count == 0)
        {
            errors.Add($"Project '{project}': 'repos' must list at least one repo name.");
            return null;
        }

        var resolved = new List<RepoConnection>(repoNames.Count);
        var anyMissing = false;
        foreach (var n in repoNames)
        {
            if (repos.TryGetValue(n, out var r)) { resolved.Add(r); continue; }
            errors.Add($"Project '{project}': references repo '{n}' which is not defined in repos: catalog.");
            anyMissing = true;
        }
        return anyMissing ? null : resolved;
    }

    private static ResolvedProject CreateProject(
        string name, RawProjectEntry raw,
        AgentConfig agent, TrackerConnection tracker, IReadOnlyList<RepoConnection> repos) =>
        new()
        {
            Name = name,
            Agent = agent,
            Tracker = tracker,
            Repos = repos,
            Pipeline = raw.Pipeline,
            Pipelines = raw.Pipelines,
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
