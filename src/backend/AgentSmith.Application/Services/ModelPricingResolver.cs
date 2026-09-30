using AgentSmith.Application.Services.Pricing;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0176b: default <see cref="IModelPricingResolver"/> implementation. Its base table is
/// the bundled price list (<see cref="IBundledModelPriceList"/>): exact id, then the bare
/// name of a provider-prefixed id, then the longest listed name the model id starts with,
/// so a dated response id (gpt-4.1-2025-04-14) still prices. An agent's pricing table
/// rides on top via <see cref="OverlayModelPricingResolver"/>.
/// </summary>
public sealed class ModelPricingResolver(IReadOnlyDictionary<string, ModelPricing> pricing) : IModelPricingResolver
{
    public ModelPricingResolver() : this(new BundledModelPriceList()) { }

    public ModelPricingResolver(IBundledModelPriceList priceList) : this(priceList.PricingByName) { }

    public ModelPricing? Resolve(string model)
    {
        if (pricing.TryGetValue(model, out var exact)) return exact;
        return pricing
            .Where(kv => model.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Key.Length)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }
}
