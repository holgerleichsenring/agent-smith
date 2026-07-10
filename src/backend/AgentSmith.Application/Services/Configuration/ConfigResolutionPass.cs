using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// p0270a: the single config resolution pass. Owns the timeout + cost-cap
/// override arithmetic (moved out of SandboxGlobalConfig / PipelineCostCapConfig)
/// and composes the per-aspect resolvers for resources + images so every
/// effective value is produced exactly once, with provenance.
/// </summary>
public sealed class ConfigResolutionPass : IConfigResolver
{
    private readonly SandboxGlobalConfig _global;
    private readonly ISandboxResourceResolver _resourceResolver;
    private readonly IAgentImageResolver _agentImageResolver;
    private readonly IOrchestratorImageResolver _orchestratorImageResolver;
    private readonly AgentSmithConfig _config;
    private readonly Lazy<ResolvedConfig> _materialized;

    public ConfigResolutionPass(
        IOptions<SandboxGlobalConfig> sandboxGlobal,
        ISandboxResourceResolver resourceResolver,
        IAgentImageResolver agentImageResolver,
        IOrchestratorImageResolver orchestratorImageResolver,
        AgentSmithConfig config)
    {
        _global = sandboxGlobal.Value;
        _resourceResolver = resourceResolver;
        _agentImageResolver = agentImageResolver;
        _orchestratorImageResolver = orchestratorImageResolver;
        _config = config;
        _materialized = new Lazy<ResolvedConfig>(BuildMaterialized);
    }

    public ResolvedProjectSettings Resolve(ResolvedProject project) => new(
        ProjectName: project.Name,
        StepTimeoutSeconds: ResolveStepTimeout(project),
        RunCommandTimeoutSeconds: ResolveRunCommandTimeout(project),
        SandboxResources: ResolveResources(project),
        AgentImage: ResolvedValue<string>.From(_agentImageResolver.Resolve(project), IsAgentOverride(project)),
        OrchestratorImage: ResolvedValue<string>.From(_orchestratorImageResolver.Resolve(project), IsOrchestratorOverride(project)),
        ToolchainImage: ResolveToolchain(project),
        CostCap: ResolveCostCap(project.Pipeline));

    public ResolvedValue<CostCapValues> ResolveCostCap(string? pipelineName)
    {
        var cap = _config.PipelineCostCap;
        return pipelineName is not null && cap.PerPipeline.TryGetValue(pipelineName, out var values)
            ? ResolvedValue<CostCapValues>.Override(values)
            : ResolvedValue<CostCapValues>.Global(cap.Default);
    }

    public ResolvedConfig Materialize() => _materialized.Value;

    private ResolvedConfig BuildMaterialized()
    {
        var projects = new Dictionary<string, ResolvedProjectSettings>(StringComparer.Ordinal);
        foreach (var (name, project) in _config.Projects)
        {
            projects[name] = ResolveResilient(project);
        }
        return new ResolvedConfig(projects);
    }

    // Dashboard path: a per-project config error (e.g. missing image version) is
    // captured, never thrown — observing the config must not crash the server.
    // The run path (Resolve) still fails loud at spawn time.
    private ResolvedProjectSettings ResolveResilient(ResolvedProject project)
    {
        try
        {
            return Resolve(project);
        }
        catch (Exception ex)
        {
            return new ResolvedProjectSettings(
                project.Name, ResolveStepTimeout(project), ResolveRunCommandTimeout(project),
                ResolveResources(project),
                new ResolvedValue<string>(null!, ResolutionSource.GlobalDefault),
                new ResolvedValue<string>(null!, ResolutionSource.GlobalDefault),
                ResolveToolchain(project), ResolveCostCap(project.Pipeline),
                ResolutionError: ex.Message);
        }
    }

    public ResolvedValue<int> ResolveStepTimeout(ResolvedProject p) =>
        p.Sandbox?.StepTimeoutSeconds is { } v
            ? ResolvedValue<int>.Override(v)
            : ResolvedValue<int>.Global(_global.StepTimeoutSeconds);

    public ResolvedValue<int> ResolveRunCommandTimeout(ResolvedProject p) =>
        p.Sandbox?.RunCommandTimeoutSeconds is { } v
            ? ResolvedValue<int>.Override(v)
            : ResolvedValue<int>.Global(_global.RunCommandTimeoutSeconds);

    // p0320a: sized for the project's configured pipeline — the snapshot shows what a
    // run of THAT pipeline would get (light profile when it is not code-changing).
    private ResolvedValue<ResourceLimits> ResolveResources(ResolvedProject p) =>
        ResolvedValue<ResourceLimits>.From(_resourceResolver.Resolve(p, p.Pipeline), p.Sandbox?.Resources is not null);

    private static ResolvedValue<string> ResolveToolchain(ResolvedProject p) =>
        string.IsNullOrEmpty(p.Sandbox?.ToolchainImage)
            ? ResolvedValue<string>.PerRun()
            : ResolvedValue<string>.Override(p.Sandbox!.ToolchainImage!);

    private static bool IsAgentOverride(ResolvedProject p) =>
        !string.IsNullOrEmpty(p.Sandbox?.AgentRegistry) || !string.IsNullOrEmpty(p.Sandbox?.AgentVersion);

    private static bool IsOrchestratorOverride(ResolvedProject p) =>
        !string.IsNullOrEmpty(p.Orchestrator?.Registry) || !string.IsNullOrEmpty(p.Orchestrator?.Version);
}
