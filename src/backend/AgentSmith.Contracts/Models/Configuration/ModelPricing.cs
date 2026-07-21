namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Per-model pricing in USD per million tokens.
/// </summary>
public sealed class ModelPricing
{
    /// <summary>
    /// p0361: Anthropic bills prompt-cache writes at a premium over the plain
    /// input rate. This is the 5-minute-TTL factor — the only TTL agent-smith
    /// requests today. If a builder ever opts into 1-hour TTL the factor for
    /// those writes is 2.0, and pricing must learn the TTL alongside it.
    /// </summary>
    public const decimal CacheWritePremium5mTtl = 1.25m;

    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal CacheReadPerMillion { get; set; }
}
