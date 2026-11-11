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
    /// p0341c: map the estimated tier to this run's PipelineCostCap, applied on the live tracker and published.
    /// Unknown tier => the resolved cap stays (fail-safe). p0413a: the tier RAISES the cap configuration resolved,
    /// never replaces it — an estimate is a guess and must not overrule a per-pipeline override downwards.
    /// </summary>
    public Task<CostCapValues?> ApplyTierAsync(
        PipelineContext pipeline, ComplexityTier tier, CancellationToken cancellationToken) =>
        tier == ComplexityTier.Unknown
            ? Task.FromResult<CostCapValues?>(null)
            : ApplyCapAsync(pipeline, tier, ResolvedCap(pipeline).RaisedTo(config.PipelineCostCap.ForTier(tier)), cancellationToken);

    /// <summary>2026-11-11-205fc: a tier and the cap it sized, applied and published — DesignEffortCap bounds its own.</summary>
    public async Task<CostCapValues?> ApplyCapAsync(
        PipelineContext pipeline, ComplexityTier tier, CostCapValues cap, CancellationToken cancellationToken)
    {
        pipeline.Set("PipelineCostCap", cap);
        // 2026-09-22-7c41a: the TIER travels with the cap it sized; the cap cannot cross a park.
        pipeline.Set(ContextKeys.ComplexityTier, tier);
        PipelineCostTracker.GetOrCreate(pipeline).ApplyCostCap(cap);
        Record(pipeline,
            $"Complexity tier: {tier.ToString().ToLowerInvariant()} — "
            + $"cost cap sized to ${cap.Usd:0.##} / {cap.Tokens:N0} tokens");
        await PublishAsync(
            pipeline,
            runId => new RunBudgetResolvedEvent(
                runId, tier.ToString().ToLowerInvariant(), cap.Usd, cap.Tokens, DateTimeOffset.UtcNow),
            cancellationToken);
        return cap;
    }

    /// <summary>
    /// p0413: the SHAPE reaches the two places that can act on it — the pipeline
    /// context, where the spec derivation reads it to decide how few specs the
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

    // The cap this run already resolved (the per-pipeline override or the static default); the default when none.
    internal CostCapValues ResolvedCap(PipelineContext pipeline) =>
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
