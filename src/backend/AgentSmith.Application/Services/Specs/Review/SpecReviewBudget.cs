using System.Globalization;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: how much of the run the spec review may use. <see cref="Spent"/> — the run's spend (USD
/// and cache-weighted tokens against the cap's arms) or its time since it started has reached
/// budget_share_percent of the cap or of the wall-time ceiling; the run's whole spend counts, the derivation's
/// included, because the share bounds what is left for building. It has no exemption: the caller decides
/// which spec the share does not stop. <see cref="Exhausted"/> — the run cap itself or the wall-time ceiling,
/// what bounds even the first reviewed spec. Each answers the reason, or null.
/// </summary>
public static class SpecReviewBudget
{
    public static string? Spent(PipelineContext pipeline, SpecReviewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(settings);
        var share = settings.Review.BudgetSharePercent;
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        var (usd, tokens) = (tracker.EstimateCostUsd(), tracker.EffectiveBudgetTokens);
        if (tracker.CostCap is { } cap && (usd * 100 >= cap.Usd * share || tokens * 100 >= cap.Tokens * share))
            return string.Create(CultureInfo.InvariantCulture, $"review has used its {share}% share of the run's "
                + $"cost cap (${usd:0.00##} / {tokens:N0} tokens of ${cap.Usd:0.00##} / {cap.Tokens:N0})");
        return Elapsed(pipeline) is { } elapsed && elapsed * 100 >= settings.MaxRunWallTimeSeconds * (double)share
            ? string.Create(CultureInfo.InvariantCulture, $"review has used its {share}% share of the run's "
                + $"wall clock ({elapsed:0} s of {settings.MaxRunWallTimeSeconds} s)")
            : null;
    }

    public static string? Exhausted(PipelineContext pipeline, SpecReviewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(settings);
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        if (tracker.IsBudgetExhausted) return CostCapStop.Describe(tracker);
        return Elapsed(pipeline) is { } elapsed && elapsed >= settings.MaxRunWallTimeSeconds
            ? $"the run's wall-time ceiling ({settings.MaxRunWallTimeSeconds} s) is reached"
            : null;
    }

    private static double? Elapsed(PipelineContext pipeline) =>
        pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var started)
            ? (DateTimeOffset.UtcNow - started).TotalSeconds
            : null;
}
