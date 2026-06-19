using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0176b: default <see cref="IModelPricingResolver"/> implementation.
/// Holds the same baseline price table that previously lived on
/// PipelineCostTracker as a private static; project pricing overrides
/// still ride on top inside the tracker.
/// </summary>
public sealed class ModelPricingResolver : IModelPricingResolver
{
    public static readonly IReadOnlyDictionary<string, ModelPricing> DefaultPricing
        = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-sonnet-4-20250514"] = new() { InputPerMillion = 3.0m, OutputPerMillion = 15.0m, CacheReadPerMillion = 0.30m },
        ["claude-haiku-4-5-20251001"] = new() { InputPerMillion = 0.80m, OutputPerMillion = 4.0m, CacheReadPerMillion = 0.08m },
        ["claude-opus-4-20250514"] = new() { InputPerMillion = 15.0m, OutputPerMillion = 75.0m },
        // p0274: gpt-5.1 (Azure OpenAI Global Standard, USD/1M). Keeps the built-in
        // fallback current; an agent's `pricing` config still overrides this.
        ["gpt-5.1"] = new() { InputPerMillion = 1.25m, OutputPerMillion = 10.0m, CacheReadPerMillion = 0.13m },
        ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m, CacheReadPerMillion = 0.50m },
        ["gpt-4.1-mini"] = new() { InputPerMillion = 0.40m, OutputPerMillion = 1.60m, CacheReadPerMillion = 0.10m },
        ["gpt-4.1-nano"] = new() { InputPerMillion = 0.10m, OutputPerMillion = 0.40m, CacheReadPerMillion = 0.025m },
        ["gpt-4o"] = new() { InputPerMillion = 2.50m, OutputPerMillion = 10.0m },
        ["gpt-4o-mini"] = new() { InputPerMillion = 0.15m, OutputPerMillion = 0.60m },
        ["llama-3.3-70b-versatile"] = new() { InputPerMillion = 0.0m, OutputPerMillion = 0.0m },
    };

    private readonly IReadOnlyDictionary<string, ModelPricing> _pricing;

    public ModelPricingResolver() : this(DefaultPricing) { }

    public ModelPricingResolver(IReadOnlyDictionary<string, ModelPricing> pricing)
    {
        _pricing = pricing;
    }

    public ModelPricing? Resolve(string model)
    {
        if (_pricing.TryGetValue(model, out var exact)) return exact;
        return _pricing
            .Where(kv => model.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Key.Length)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }
}
