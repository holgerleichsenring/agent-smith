namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Pricing configuration for LLM models. Prices in USD per million tokens.
/// </summary>
public class PricingConfig
{
    public Dictionary<string, ModelPricing> Models { get; set; } = new();
}

/// <summary>
/// Per-model pricing in USD per million tokens.
/// </summary>
public class ModelPricing
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal CacheReadPerMillion { get; set; }
}
