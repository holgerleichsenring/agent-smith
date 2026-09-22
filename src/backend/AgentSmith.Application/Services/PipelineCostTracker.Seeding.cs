using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-22-7c41a: how a run's cost tracker comes into being, and what it inherits from
/// the segment before a park. The tracker itself cannot cross a checkpoint — it holds a
/// lock, a pricing resolver, a scope manager and a worker ledger, and a serialized copy
/// would come back with zero counters, a default resolver and no cap in place of the live
/// one. What crosses is a <see cref="PriorSpendSnapshot"/>, and this is where it lands.
/// </summary>
public sealed partial class PipelineCostTracker
{
    /// <summary>
    /// The cap this tracker measures against, so a reader that STOPS a run on it can say
    /// WHICH number it stopped on. Null when the run has no cap configured (fail-open).
    /// </summary>
    public CostCapValues? CostCap { get { lock (_gate) return _costCap; } }

    /// <summary>
    /// The spend so far, as the raw buckets the cap's own arithmetic re-weights. Taken
    /// AFTER any seeding, so a run parked a second time carries the whole run rather than
    /// the last segment.
    /// </summary>
    public PriorSpendSnapshot CaptureSpend()
    {
        lock (_gate)
            return new PriorSpendSnapshot(
                _totalInputTokens, _totalOutputTokens, _totalCacheCreateTokens,
                _totalCacheReadTokens, _accruedUsd,
                _workerCalls.InputTokens, _workerCalls.OutputTokens, _workerCalls.CacheReadTokens,
                _workerCalls.CacheCreationTokens, _workerCalls.ReportedCostUsd,
                _workerCalls.CallCount == 0 ? string.Empty : _workerCalls.Models);
    }

    /// <summary>
    /// Adds a prior segment's spend so the cap is compared against the whole run. The CALL
    /// COUNT is deliberately not seeded: a count is of calls this segment made, and
    /// inflating it would fabricate rows in result.md for calls this segment never placed.
    /// The consequence is named and accepted — a resumed segment that places no call of its
    /// own renders no cost section, while the run row's total stays the run's.
    /// </summary>
    public void SeedPriorSpend(PriorSpendSnapshot prior)
    {
        ArgumentNullException.ThrowIfNull(prior);
        lock (_gate)
        {
            _totalInputTokens += prior.InputTokens;
            _totalOutputTokens += prior.OutputTokens;
            _totalCacheCreateTokens += prior.CacheCreateTokens;
            _totalCacheReadTokens += prior.CacheReadTokens;
            _accruedUsd += prior.AccruedUsd;
            _workerCalls.Seed(
                prior.WorkerInputTokens, prior.WorkerOutputTokens, prior.WorkerCacheReadTokens,
                prior.WorkerCacheCreationTokens, prior.WorkerReportedCostUsd, prior.WorkerModels);
        }
    }

    /// <summary>
    /// The run's one tracker, created from the context on first ask. The create branch is
    /// where a carried <see cref="PriorSpendSnapshot"/> is read — exactly once per run,
    /// because every later caller gets the instance this one stored.
    /// </summary>
    public static PipelineCostTracker GetOrCreate(PipelineContext pipeline)
    {
        const string Key = "PipelineCostTracker";
        if (pipeline.TryGet<PipelineCostTracker>(Key, out var existing)
            && existing is not null)
            return existing;

        pipeline.TryGet<IModelPricingResolver>("ModelPricingResolver", out var resolver);
        pipeline.TryGet<PricingConfig>("ProjectPricing", out var pricingConfig);
        pipeline.TryGet<CostCapValues>("PipelineCostCap", out var costCap);
        var tracker = new PipelineCostTracker(resolver, pricingConfig, costCap);
        if (pipeline.TryGet<PriorSpendSnapshot>(ContextKeys.PriorSpend, out var prior)
            && prior is not null)
            tracker.SeedPriorSpend(prior);
        pipeline.Set(Key, tracker);
        return tracker;
    }
}
