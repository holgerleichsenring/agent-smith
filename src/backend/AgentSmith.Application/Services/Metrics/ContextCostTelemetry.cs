using AgentSmith.Contracts.Progress;

namespace AgentSmith.Application.Services.Metrics;

/// <summary>
/// p0356: computes the <see cref="ContextCostReport"/> from the pipeline cost
/// tracker's totals (per-call usage incl. cache buckets, live since p0323) and
/// the run's progress ledger. Pure — the handler logs the result; the run cost
/// summary already persists the underlying token buckets.
/// </summary>
public static class ContextCostTelemetry
{
    public static ContextCostReport Compute(long totalTokens, long cacheReadTokens, ProgressLedger ledger)
    {
        var doneItems = ledger.Entries.Count(e => e.Status == ProgressStatus.Done);
        var cachedShare = totalTokens > 0
            ? Math.Clamp((double)cacheReadTokens / totalTokens, 0d, 1d)
            : 0d;
        var tokensPerItem = doneItems > 0 ? totalTokens / doneItems : (long?)null;
        return new ContextCostReport(totalTokens, cachedShare, doneItems, tokensPerItem);
    }
}
