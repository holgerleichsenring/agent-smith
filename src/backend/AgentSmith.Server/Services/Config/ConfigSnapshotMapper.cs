using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0266: projects the in-memory <see cref="AgentSmithConfig"/> onto the
/// redacted <see cref="ConfigSnapshot"/> the dashboard reads. Pure field
/// allow-list — every property written here is display-safe; secret-bearing
/// fields (Secrets, ApiKeySecret, Auth, ConnectionString) are simply never
/// referenced. Reachability edges are delegated to <see cref="ConfigEdgeBuilder"/>.
/// </summary>
public static class ConfigSnapshotMapper
{
    public static ConfigSnapshot ToSnapshot(AgentSmithConfig config)
    {
        var projects = config.Projects
            .Select(kv => MapProject(kv.Key, kv.Value, config)).ToList();
        return new ConfigSnapshot(
            Agents: config.Agents.Select(kv => MapAgent(kv.Key, kv.Value)).ToList(),
            Repos: config.Repos.Values.Select(MapRepo).ToList(),
            Trackers: config.Trackers.Values.Select(MapTracker).ToList(),
            Projects: projects,
            Edges: ConfigEdgeBuilder.Build(projects),
            Globals: MapGlobals(config));
    }

    private static ConfigAgent MapAgent(string name, AgentConfig agent) => new(
        Name: name,
        Type: agent.Type,
        Model: agent.Model,
        NetworkTimeoutSeconds: agent.NetworkTimeoutSeconds,
        MaxFixIterations: agent.MaxFixIterations,
        RequestsPerMinute: agent.RateLimit?.RequestsPerMinute,
        InputTokensPerMinute: agent.RateLimit?.InputTokensPerMinute,
        MaxConcurrentSkillRounds: agent.Parallelism.MaxConcurrentSkillRounds);

    private static ConfigRepo MapRepo(RepoConnection repo) => new(
        Name: repo.Name,
        Type: repo.Type.ToString(),
        Host: HostOf(repo.Url),
        DefaultBranch: repo.DefaultBranch);

    private static ConfigTracker MapTracker(TrackerConnection tracker) => new(
        Name: tracker.Name,
        Type: tracker.Type.ToString(),
        Project: tracker.Project,
        OpenStates: tracker.OpenStates,
        DoneStatus: tracker.DoneStatus);

    private static ConfigProject MapProject(string name, ResolvedProject project, AgentSmithConfig config) => new(
        Name: name,
        Pipeline: project.Pipeline,
        AgentName: AgentNameOf(project, config),
        TrackerName: project.Tracker.Name,
        RepoNames: project.Repos.Select(r => r.Name).ToList(),
        Pipelines: PipelineNamesOf(project));

    private static ConfigGlobals MapGlobals(AgentSmithConfig config) => new(
        Sandbox: new ConfigSandbox(
            config.Sandbox.AgentRegistry, config.Sandbox.AgentVersion,
            config.Sandbox.StepTimeoutSeconds, config.Sandbox.RunCommandTimeoutSeconds),
        Orchestrator: new ConfigOrchestrator(
            config.Orchestrator.Registry, config.Orchestrator.Version,
            config.Orchestrator.MaxRunWallTimeSeconds),
        Limits: new ConfigLimits(
            config.Limits.MaxToolCallsPerSkill, config.Limits.MaxLlmCallsPerSkill,
            config.Limits.MaxConcurrentSkillCalls, config.Limits.MaxSubAgentsPerRun),
        CostCap: new ConfigCostCap(config.PipelineCostCap.Default.Usd, config.PipelineCostCap.Default.Tokens),
        PersistenceProvider: config.Persistence.Provider);

    // The resolver stores the SAME AgentConfig instance the catalog holds
    // (ResolvedProjectBuilder.ResolveAgent), so reference equality recovers the
    // catalog name. Falls back to a "type/model" label for an inline agent.
    private static string AgentNameOf(ResolvedProject project, AgentSmithConfig config)
    {
        foreach (var (name, agent) in config.Agents)
        {
            if (ReferenceEquals(agent, project.Agent)) return name;
        }
        return $"{project.Agent.Type}/{project.Agent.Model}";
    }

    private static IReadOnlyList<string> PipelineNamesOf(ResolvedProject project)
    {
        if (project.Pipelines.Count > 0) return project.Pipelines.Select(p => p.Name).ToList();
        return string.IsNullOrEmpty(project.Pipeline) ? [] : [project.Pipeline];
    }

    private static string? HostOf(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
}
