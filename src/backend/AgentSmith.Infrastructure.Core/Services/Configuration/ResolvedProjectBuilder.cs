using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Builds one <see cref="ResolvedProject"/> from a <see cref="RawProjectEntry"/>
/// plus the already-built agents/repos/trackers catalogs.
///
/// p0391b: an unresolved name reference is one blocking <see cref="StartupFinding"/> on
/// this project, naming the field that carries the bad name. The project itself drops out
/// (it cannot be run without its agent, tracker or repos); the rest of the configuration
/// materializes and keeps working.
/// </summary>
public sealed class ResolvedProjectBuilder(ProjectRepoResolver repoResolver)
{
    public ResolvedProjectBuilder() : this(new ProjectRepoResolver(new ConnectionRepoUrlBuilder())) { }

    public ResolvedProject? TryBuild(
        string name,
        RawProjectEntry raw,
        ConfigCatalogs catalogs,
        RepoGlobExpander? globExpander,
        List<StartupFinding> findings)
    {
        var agent = ResolveAgent(name, raw.Agent, catalogs.Agents, findings);
        var tracker = ResolveTracker(name, raw.Tracker, catalogs.Trackers, findings);
        var repoList = repoResolver.Resolve(
            name, raw.Repos, catalogs.Repos, catalogs.Connections, globExpander, findings);
        var pipelines = ResolvePipelines(name, raw.Pipelines, catalogs.Agents, findings);

        if (agent is null || tracker is null || repoList is null || pipelines is null) return null;

        return CreateProject(name, raw, agent, tracker, repoList, pipelines);
    }

    private static IReadOnlyList<PipelineDefinition>? ResolvePipelines(
        string project, IReadOnlyList<RawPipelineEntry> raws,
        IReadOnlyDictionary<string, AgentConfig> agents, List<StartupFinding> findings)
    {
        var result = new List<PipelineDefinition>(raws.Count);
        var anyError = false;
        foreach (var r in raws)
        {
            if (string.IsNullOrEmpty(r.Name))
            {
                findings.Add(ProjectFindings.Blocking(project, "pipelines",
                    $"Project '{project}': pipelines entry is missing required field 'name'."));
                anyError = true;
                continue;
            }

            AgentConfig? resolvedAgent = null;
            if (!string.IsNullOrEmpty(r.Agent))
            {
                if (!agents.TryGetValue(r.Agent, out resolvedAgent))
                {
                    findings.Add(ProjectFindings.Blocking(project, "pipelines",
                        $"Project '{project}': pipeline '{r.Name}' references agent '{r.Agent}' " +
                        "which is not defined in agents: catalog."));
                    anyError = true;
                }
            }

            if (r.ConfidenceThreshold is < 0 or > 100)
            {
                findings.Add(ProjectFindings.Blocking(project, "pipelines",
                    $"Project '{project}': pipeline '{r.Name}' has confidence_threshold " +
                    $"{r.ConfidenceThreshold} — must be between 0 and 100."));
                anyError = true;
            }

            result.Add(new PipelineDefinition
            {
                Name = r.Name,
                AgentName = string.IsNullOrEmpty(r.Agent) ? null : r.Agent,
                Agent = resolvedAgent,
                SkillsPath = r.SkillsPath,
                CodingPrinciplesPath = r.CodingPrinciplesPath,
                ConfidenceThreshold = r.ConfidenceThreshold,
            });
        }
        return anyError ? null : result;
    }

    private static AgentConfig? ResolveAgent(
        string project, string agentName,
        IReadOnlyDictionary<string, AgentConfig> agents, List<StartupFinding> findings)
    {
        if (string.IsNullOrEmpty(agentName))
        {
            findings.Add(ProjectFindings.Blocking(project, "agent",
                $"Project '{project}': missing required reference 'agent'."));
            return null;
        }
        if (agents.TryGetValue(agentName, out var agent)) return agent;

        findings.Add(ProjectFindings.Blocking(project, "agent",
            $"Project '{project}': references agent '{agentName}' which is not defined in agents: catalog."));
        return null;
    }

    private static TrackerConnection? ResolveTracker(
        string project, string trackerName,
        IReadOnlyDictionary<string, TrackerConnection> trackers, List<StartupFinding> findings)
    {
        if (string.IsNullOrEmpty(trackerName))
        {
            findings.Add(ProjectFindings.Blocking(project, "tracker",
                $"Project '{project}': missing required reference 'tracker'."));
            return null;
        }
        if (trackers.TryGetValue(trackerName, out var tracker)) return tracker;

        findings.Add(ProjectFindings.Blocking(project, "tracker",
            $"Project '{project}': references tracker '{trackerName}' which is not defined in trackers: catalog."));
        return null;
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
