namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Pricing configuration for LLM models. Prices in USD per million tokens.
/// </summary>
public sealed class PricingConfig
{
    public Dictionary<string, ModelPricing> Models { get; set; } = new();
}
