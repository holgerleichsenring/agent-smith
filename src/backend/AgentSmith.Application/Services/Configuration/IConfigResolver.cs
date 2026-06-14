using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// p0270a: the SINGLE home of config override-resolution. The run path and the
/// dashboard both go through it, so there is one resolution — never a second
/// computation that could drift from what actually runs. Replaces the scattered
/// on-demand arithmetic (SandboxGlobalConfig.Resolve*, PipelineCostCapConfig.ResolveFor)
/// and composes the per-aspect resolvers (resources / agent image / orchestrator
/// image) so their logic stays single-sourced too.
/// </summary>
public interface IConfigResolver
{
    /// <summary>Full effective settings + provenance for one project. Eagerly
    /// resolves images, so it can throw on a missing image version — used by
    /// <see cref="Materialize"/> (which catches) and tests. The RUN path reads the
    /// granular timeout accessors below instead, so a run that never spawns an
    /// orchestrator is not forced to resolve its image early.</summary>
    ResolvedProjectSettings Resolve(ResolvedProject project);

    /// <summary>Effective per-step wall-time cap (override ?? global). Cheap, never throws.</summary>
    ResolvedValue<int> ResolveStepTimeout(ResolvedProject project);

    /// <summary>Effective default run_command timeout (override ?? global). Cheap, never throws.</summary>
    ResolvedValue<int> ResolveRunCommandTimeout(ResolvedProject project);

    /// <summary>Effective cost cap for a pipeline (per-pipeline override ?? default).</summary>
    ResolvedValue<CostCapValues> ResolveCostCap(string? pipelineName);

    /// <summary>The materialized desired-state over every configured project, built
    /// once and cached. Resilient — a per-project config error is captured in
    /// <see cref="ResolvedProjectSettings.ResolutionError"/> instead of throwing,
    /// so observing the config never crashes the server.</summary>
    ResolvedConfig Materialize();
}
