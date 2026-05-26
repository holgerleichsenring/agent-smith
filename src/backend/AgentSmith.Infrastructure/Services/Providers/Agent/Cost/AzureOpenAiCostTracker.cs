using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Cost tracker for Azure OpenAI. Response shape is identical to OpenAI's
/// ChatCompletion (Azure SDK reuses the OpenAI Chat types), so behavior is
/// inherited unchanged. Distinct type kept for explicit DI/wire-up and to
/// allow future Azure-specific tweaks (e.g. deployment-name-aware pricing).
/// </summary>
public sealed class AzureOpenAiCostTracker : OpenAiCostTracker
{
    public AzureOpenAiCostTracker(PricingConfig pricing, ILogger logger, TokenUsageTracker? tokenTracker = null)
        : base(pricing, logger, tokenTracker)
    {
    }
}
