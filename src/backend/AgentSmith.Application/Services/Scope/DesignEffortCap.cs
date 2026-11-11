using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// 2026-11-11-205fc: a design turn's stated effort sizes its cost cap within two bounds — never below the cap the run
/// resolved, never above a per-pipeline override the operator configured for the turn's pipeline. A coding run's scope
/// estimate keeps its own rule (p0413a: raise only) through <see cref="ScopeEstimateRecorder.ApplyTierAsync"/>.
/// </summary>
public sealed class DesignEffortCap(AgentSmithConfig config, ScopeEstimateRecorder recorder)
{
    /// <summary>The cap the effort sized, applied and published with its tier; null for an unknown tier.</summary>
    public Task<CostCapValues?> ApplyAsync(
        PipelineContext pipeline, ComplexityTier tier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (tier == ComplexityTier.Unknown) return Task.FromResult<CostCapValues?>(null);
        var resolved = recorder.ResolvedCap(pipeline);
        var raised = resolved.RaisedTo(config.PipelineCostCap.ForTier(tier));
        var cap = Override(pipeline) is { } ceiling ? raised.LoweredTo(ceiling.RaisedTo(resolved)) : raised;
        return recorder.ApplyCapAsync(pipeline, tier, cap, cancellationToken);
    }

    private CostCapValues? Override(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.PipelineName, out var name) && name is not null
        && config.PipelineCostCap.PerPipeline.TryGetValue(name, out var values)
            ? values
            : null;
}
