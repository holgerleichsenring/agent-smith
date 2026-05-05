using AgentSmith.Contracts.Models.Configuration;
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
}
