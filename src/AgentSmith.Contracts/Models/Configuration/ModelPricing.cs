namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Per-model pricing in USD per million tokens.
/// </summary>
public sealed class ModelPricing
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal CacheReadPerMillion { get; set; }
}
