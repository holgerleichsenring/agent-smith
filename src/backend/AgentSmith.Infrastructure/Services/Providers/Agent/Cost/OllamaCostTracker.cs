using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Cost tracker for local Ollama runtimes. No caching, no remote billing —
/// pricing config typically has zeros for Ollama models so cost stays at $0,
/// but token counts are still tracked for visibility.
/// </summary>
public sealed class OllamaCostTracker : CostTrackerBase
{
    public OllamaCostTracker(PricingConfig pricing, ILogger logger, TokenUsageTracker? tokenTracker = null)
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
