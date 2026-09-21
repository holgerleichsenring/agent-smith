using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// 2026-09-22-7c41a: a resumed run's EFFECTIVE cost cap. The launch seeds the configured
/// one on every launch, a resume included; the raised one the scope step derived from the
/// complexity tier is excluded from the checkpoint, and the scope step is a completed
/// command the resume never re-runs. So a run sized to a large tier came back on the
/// static default and died on the token arm at a fraction of the cap its own row records.
/// <para>
/// The TIER crosses the park, not the computed cap: recomputing reproduces exactly what
/// scope did, spends no model call, and honours a cap table or per-pipeline entry the
/// operator changed while the run was parked. The raise stays a MERGE — the configuration
/// as it stands now, raised to the tier — so an estimate can still never shrink the
/// operator's instruction.
/// </para>
/// </summary>
public sealed class ResumedCapRecompute(
    IConfigResolver configResolver,
    IEventPublisher eventPublisher,
    ILogger<ResumedCapRecompute> logger)
{
    /// <summary>
    /// Re-sizes the cap in the context when the run carries a tier. Runs at the one seam
    /// where the restore has landed and nothing has yet read the cap: the first
    /// PipelineCostTracker.GetOrCreate is still ahead. A run with no carried tier (or an
    /// Unknown one, and every checkpoint written before this phase) is left exactly as the
    /// launch seeded it.
    /// </summary>
    public async Task ApplyAsync(
        PipelineContext pipeline, string pipelineName, AgentSmithConfig config,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(config);
        if (!pipeline.TryGet<ComplexityTier>(ContextKeys.ComplexityTier, out var tier)
            || tier == ComplexityTier.Unknown)
            return;

        // The SAME pipeline name the launch resolved the cap with. Per-pipeline cap lookup
        // does not resolve aliases, so canonicalising here would read a different entry.
        var cap = configResolver.ResolveCostCap(pipelineName).Value
            .RaisedTo(config.PipelineCostCap.ForTier(tier));
        pipeline.Set("PipelineCostCap", cap);
        logger.LogInformation(
            "Resumed run re-sized to its recorded {Tier} tier — cost cap ${Usd:0.##} / {Tokens:N0} tokens",
            tier, cap.Usd, cap.Tokens);
        await PublishAsync(pipeline, tier, cap, cancellationToken);
    }

    // The recomputed cap may legitimately DIFFER from the one the row records, because the
    // configuration is live-reloadable and a parked run can outlive its own numbers. So the
    // run row is re-stated rather than left contradicting the cap the run now runs against.
    // A publish failure must not fail a resume: log and carry on.
    private async Task PublishAsync(
        PipelineContext pipeline, ComplexityTier tier, CostCapValues cap,
        CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        try
        {
            await eventPublisher.PublishAsync(
                new RunBudgetResolvedEvent(
                    runId!, tier.ToString().ToLowerInvariant(), cap.Usd, cap.Tokens,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to re-publish the resolved budget for run {RunId}", runId);
        }
    }
}
