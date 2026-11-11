using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854a: a design turn's spend as its proposal carries it, read off the turn's own cost tracker: what it
/// spent so far, its cap, the tier that sized the cap and whether the cap is exhausted. Null before anything was tracked.
/// </summary>
public static class TurnSpend
{
    private const string TrackerKey = "PipelineCostTracker";

    public static ProposalSpend? Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<PipelineCostTracker>(TrackerKey, out var tracker) || tracker is null) return null;
        var tier = pipeline.TryGet<ComplexityTier>(ContextKeys.ComplexityTier, out var t) && t != ComplexityTier.Unknown
            ? t.ToString().ToLowerInvariant()
            : null;
        return new ProposalSpend(tracker.EstimateCostUsd(), tracker.CostCap?.Usd, tier, tracker.IsBudgetExhausted);
    }
}
