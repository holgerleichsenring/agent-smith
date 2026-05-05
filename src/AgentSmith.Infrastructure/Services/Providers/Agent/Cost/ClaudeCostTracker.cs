using AgentSmith.Contracts.Models.Configuration;
using Anthropic.SDK.Messaging;
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
}
