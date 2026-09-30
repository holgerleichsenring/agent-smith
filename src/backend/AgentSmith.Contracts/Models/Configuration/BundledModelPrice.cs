namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// One model's price as the bundled price list carries it, in USD per million tokens.
/// <see cref="CacheReadPerMillion"/> and <see cref="ContextWindowTokens"/> are null
/// where the list states none.
/// </summary>
public sealed record BundledModelPrice(
    string Id,
    string Provider,
    decimal InputPerMillion,
    decimal OutputPerMillion,
    decimal? CacheReadPerMillion,
    int? ContextWindowTokens)
{
    /// <summary>The runtime's pricing shape; a missing cache-read rate prices cache reads at zero.</summary>
    public ModelPricing ToPricing() => new()
    {
        InputPerMillion = InputPerMillion,
        OutputPerMillion = OutputPerMillion,
        CacheReadPerMillion = CacheReadPerMillion ?? 0m,
    };
}
