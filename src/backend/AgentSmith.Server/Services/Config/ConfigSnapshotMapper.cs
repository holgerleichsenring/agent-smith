using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0266/p0270a: projects the in-memory <see cref="AgentSmithConfig"/> plus the
/// materialized <see cref="ResolvedConfig"/> onto the redacted
/// <see cref="ConfigSnapshot"/> the dashboard reads. Pure field allow-list —
/// secret-bearing fields (Secrets, ApiKeySecret, Auth, ConnectionString) are
/// never referenced. The per-project effective settings come straight from the
/// single <see cref="IConfigResolver"/>, so the dashboard shows exactly what the
/// run path resolves — no second computation.
/// </summary>
public static class ConfigSnapshotMapper
{
    public static ConfigSnapshot ToSnapshot(AgentSmithConfig config, IConfigResolver resolver)
    {
        var resolved = resolver.Materialize();
        var projects = config.Projects
            .Select(kv => MapProject(kv.Key, kv.Value, config, resolved)).ToList();
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

    private static ConfigProject MapProject(
        string name, ResolvedProject project, AgentSmithConfig config, ResolvedConfig resolved) => new(
        Name: name,
        Pipeline: project.Pipeline,
        AgentName: AgentNameOf(project, config),
        TrackerName: project.Tracker.Name,
        RepoNames: project.Repos.Select(r => r.Name).ToList(),
        Pipelines: PipelineNamesOf(project),
        Resolved: MapResolved(resolved.Projects[name]),
        Trigger: MapTrigger(project));

    private static ConfigResolvedSettings MapResolved(ResolvedProjectSettings s) => new(
        StepTimeoutSeconds: Rv(s.StepTimeoutSeconds),
        RunCommandTimeoutSeconds: Rv(s.RunCommandTimeoutSeconds),
        SandboxResources: new ConfigResolvedValue<ConfigResourceSummary>(
            ToSummary(s.SandboxResources.Value), Source(s.SandboxResources.Source)),
        AgentImage: Rv(s.AgentImage),
        OrchestratorImage: Rv(s.OrchestratorImage),
        ToolchainImage: Rv(s.ToolchainImage),
        CostCap: new ConfigResolvedValue<ConfigCostCapValue>(
            ToCostCap(s.CostCap.Value), Source(s.CostCap.Source)),
        ResolutionError: s.ResolutionError);

    private static ConfigTrigger MapTrigger(ResolvedProject p)
    {
        var t = p.JiraTrigger ?? p.GithubTrigger ?? p.GitlabTrigger ?? p.AzuredevopsTrigger;
        return t is not null
            ? new ConfigTrigger(t.TriggerStatuses, t.DoneStatus, t.FailedStatus, p.Polling.Enabled)
            : new ConfigTrigger(p.Tracker.OpenStates, p.Tracker.DoneStatus, null, p.Polling.Enabled);
    }

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

    private static ConfigResolvedValue<T> Rv<T>(ResolvedValue<T> v) => new(v.Value, Source(v.Source));

    private static ConfigResourceSummary? ToSummary(ResourceLimits? r) =>
        r is null ? null : new(r.CpuRequest, r.CpuLimit, r.MemoryRequest, r.MemoryLimit);

    private static ConfigCostCapValue? ToCostCap(CostCapValues? c) =>
        c is null ? null : new(c.Usd, c.Tokens);

    private static string Source(ResolutionSource s) => s switch
    {
        ResolutionSource.ProjectOverride => "override",
        ResolutionSource.RunResolved => "run-resolved",
        _ => "global-default",
    };

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
