using AgentSmith.Application.Extensions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413: the scope classifier estimates the ticket twice — its SIZE (complexity
/// tier) and its SHAPE (deterministic transformation / judgement / mixed). Both
/// are run facts, and both must leave the log: the size sizes the cost cap, the
/// shape sizes the PROCESS the derivation builds. Split out of ScopeReposHandler,
/// which owns the scoping decision, so the handler stays about repos.
/// </summary>
public sealed class ScopeEstimateRecorder(
    AgentSmithConfig config,
    IEventPublisher eventPublisher,
    ILogger<ScopeEstimateRecorder> logger)
{
    /// <summary>
    /// p0413a: record what the scope call estimated — both halves, in the order the run
    /// needs them (the ceiling before the first big spend, the shape before the cut).
    /// </summary>
    public async Task ApplyAsync(
        PipelineContext pipeline, ScopeEstimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(estimate);
        await ApplyTierAsync(pipeline, estimate.Tier, cancellationToken);
        await RecordShapeAsync(pipeline, estimate.Shape, cancellationToken);
    }

    /// <summary>
    /// p0341c: map the estimated tier to this run's effective PipelineCostCap and apply it
    /// in place. The scope-classifier call ALSO created the PipelineCostTracker (it tracks
    /// its own call), so the tier cap must be applied on the live tracker AND published for
    /// any tracker created later. Unknown tier => leave the resolved cap untouched
    /// (fail-safe); the decision is recorded as a run artifact, never silent.
    /// <para>
    /// p0413a: the tier RAISES the cap the run resolved from configuration, never replaces
    /// it. ForTier knows only the static default, so replacing would silently discard a
    /// per-pipeline override — and an estimate, which is a guess, would overrule the
    /// operator's instruction downwards.
    /// </para>
    /// </summary>
    private async Task ApplyTierAsync(
        PipelineContext pipeline, ComplexityTier tier, CancellationToken cancellationToken)
    {
        if (tier == ComplexityTier.Unknown) return;
        var cap = ResolvedCap(pipeline).RaisedTo(config.PipelineCostCap.ForTier(tier));
        pipeline.Set("PipelineCostCap", cap);
        PipelineCostTracker.GetOrCreate(pipeline).ApplyCostCap(cap);
        Record(pipeline,
            $"Complexity tier: {tier.ToString().ToLowerInvariant()} — "
            + $"cost cap sized to ${cap.Usd:0.##} / {cap.Tokens:N0} tokens");
        await PublishAsync(
            pipeline,
            runId => new RunBudgetResolvedEvent(
                runId, tier.ToString().ToLowerInvariant(), cap.Usd, cap.Tokens, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// p0413: the SHAPE reaches the two places that can act on it — the pipeline
    /// context, where the spec derivation reads it to decide how few phases the
    /// deliverable needs, and the run row, where an operator reads why the ticket
    /// got the process it got. No shape stated => nothing recorded, and every
    /// consumer behaves exactly as it did before.
    /// </summary>
    private async Task RecordShapeAsync(
        PipelineContext pipeline, WorkShapeVerdict? shape, CancellationToken cancellationToken)
    {
        if (shape is null) return;
        pipeline.Set(ContextKeys.WorkShape, shape);
        Record(pipeline, $"Work shape: {shape}");
        await PublishAsync(
            pipeline,
            runId => new RunWorkShapeResolvedEvent(
                runId, shape.Name, shape.Reason, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    // The cap this run already resolved (ExecutePipelineUseCase wrote the per-pipeline
    // override or the static default there); the static default when the run has none.
    private CostCapValues ResolvedCap(PipelineContext pipeline) =>
        pipeline.TryGet<CostCapValues>("PipelineCostCap", out var cap) && cap is not null
            ? cap
            : config.PipelineCostCap.Default;

    private void Record(PipelineContext pipeline, string record)
    {
        pipeline.AppendDecisions([new PlanDecision("scope", record)]);
        logger.LogInformation("{Record}", record);
    }

    // p0357: the estimate leaves the log and reaches the run row — the applier
    // persists it so the dashboard can answer "what will this cost (at most)" and
    // "why this process" from step 4 onward. A publish failure must not fail
    // scoping: log and continue.
    private async Task PublishAsync(
        PipelineContext pipeline, Func<string, RunEvent> compose, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        try
        {
            await eventPublisher.PublishAsync(compose(runId!), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish a scope estimate for run {RunId}", runId);
        }
    }
}
