using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Cost tracker for OpenAI. OpenAI reports prompt_tokens as the FULL input
/// (including any cached portions); the cached subset is exposed separately
/// via InputTokenDetails.CachedTokenCount and is priced at the cache-read rate.
/// This tracker subtracts cached from total to derive the canonical billable
/// input — without this, cached portions would be billed at the full input rate
/// and run cost would be over-stated by the cache-hit fraction.
/// </summary>
public class OpenAiCostTracker : CostTrackerBase
{
    public OpenAiCostTracker(PricingConfig pricing, ILogger logger, TokenUsageTracker? tokenTracker = null)
        : base(pricing, logger, tokenTracker)
    {
    }

    /// <summary>
    /// Records token usage from an OpenAI ChatCompletion.
    /// </summary>
    public void Track(ChatCompletion completion)
    {
        var usage = completion.Usage;
        if (usage is null) return;

        var cached = usage.InputTokenDetails?.CachedTokenCount ?? 0;
        var billable = usage.InputTokenCount - cached;
        if (billable < 0) billable = 0;

        Aggregate(
            billableInput: billable,
            output: usage.OutputTokenCount,
            cacheCreate: 0,
            cacheRead: cached);
    }

    /// <summary>
    /// Records raw token counts (used for compactor summarizer calls where we
    /// already have ints, no cache info).
    /// </summary>
    public void TrackRaw(int inputTokens, int outputTokens)
        => Aggregate(inputTokens, outputTokens);

    /// <summary>
    /// Records token usage from a Microsoft.Extensions.AI ChatResponse. M.E.AI.OpenAI
    /// surfaces cached_tokens via UsageDetails.AdditionalCounts['cached_tokens'].
    /// Total input minus cached gives the billable portion.
    /// </summary>
    public void Track(ChatResponse response)
    {
        if (response.Usage is null) return;
        var total = (int)(response.Usage.InputTokenCount ?? 0);
        var cached = ReadCount(response.Usage, "cached_tokens");
        var billable = Math.Max(0, total - cached);
        Aggregate(
            billableInput: billable,
            output: (int)(response.Usage.OutputTokenCount ?? 0),
            cacheCreate: 0,
            cacheRead: cached);
    }

    private static int ReadCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? (int)v : 0;
}
