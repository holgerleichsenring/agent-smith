using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Cost tracker for Google Gemini. The current GenerativeAI SDK usage in this
/// project doesn't expose context-cache information, so cached_input is always
/// zero — billable input equals reported input.
/// </summary>
public sealed class GeminiCostTracker : CostTrackerBase
{
    public GeminiCostTracker(PricingConfig pricing, ILogger logger, TokenUsageTracker? tokenTracker = null)
        : base(pricing, logger, tokenTracker)
    {
    }

    public void Track(int inputTokens, int outputTokens)
        => Aggregate(inputTokens, outputTokens);

    /// <summary>
    /// Records token usage from a Microsoft.Extensions.AI ChatResponse.
    /// </summary>
    public void Track(ChatResponse response)
    {
        if (response.Usage is null) return;
        Aggregate(
            billableInput: (int)(response.Usage.InputTokenCount ?? 0),
            output: (int)(response.Usage.OutputTokenCount ?? 0));
    }
}
