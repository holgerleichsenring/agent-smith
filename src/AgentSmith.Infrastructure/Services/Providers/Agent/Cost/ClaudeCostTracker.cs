using AgentSmith.Contracts.Models.Configuration;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Cost tracker for Anthropic Claude. Anthropic reports input_tokens as the new
/// uncached billable portion (already excluding cache reads), so the canonical
/// mapping is direct.
/// </summary>
public sealed class ClaudeCostTracker : CostTrackerBase
{
    public ClaudeCostTracker(PricingConfig pricing, ILogger logger, TokenUsageTracker? tokenTracker = null)
        : base(pricing, logger, tokenTracker)
    {
    }

    /// <summary>
    /// Records token usage from a Claude MessageResponse.
    /// </summary>
    public void Track(MessageResponse response)
    {
        if (response.Usage is null) return;

        Aggregate(
            billableInput: response.Usage.InputTokens,
            output: response.Usage.OutputTokens,
            cacheCreate: response.Usage.CacheCreationInputTokens,
            cacheRead: response.Usage.CacheReadInputTokens);
    }

    /// <summary>
    /// Records raw token counts (used by the Claude compactor's summarizer calls
    /// that are tracked outside the main response flow).
    /// </summary>
    public void TrackRaw(int inputTokens, int outputTokens, int cacheCreate = 0, int cacheRead = 0)
        => Aggregate(inputTokens, outputTokens, cacheCreate, cacheRead);

    /// <summary>
    /// Records token usage from a Microsoft.Extensions.AI ChatResponse. Anthropic
    /// surfaces cache info via AdditionalCounts['cache_read_input_tokens'] and
    /// 'cache_creation_input_tokens'.
    /// </summary>
    public void Track(ChatResponse response)
    {
        if (response.Usage is null) return;
        Aggregate(
            billableInput: (int)(response.Usage.InputTokenCount ?? 0),
            output: (int)(response.Usage.OutputTokenCount ?? 0),
            cacheCreate: ReadCount(response.Usage, "cache_creation_input_tokens"),
            cacheRead: ReadCount(response.Usage, "cache_read_input_tokens"));
    }

    private static int ReadCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? (int)v : 0;
}
