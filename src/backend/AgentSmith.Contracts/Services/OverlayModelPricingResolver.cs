using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0274: layers project-level <see cref="PricingConfig"/> overrides over a base
/// <see cref="IModelPricingResolver"/>. A configured model is matched exact-first
/// then longest-prefix; on a miss the lookup delegates to the base resolver's
/// defaults. Shared by the live per-call cost emitter (EventPublishingChatClient,
/// via ChatClientFactory) and the run-summary PipelineCostTracker, so a
/// config-priced model (e.g. gpt-5.1) is priced identically on both paths — never
/// $0 on the live view while the summary shows a real number.
/// </summary>
public sealed class OverlayModelPricingResolver : IModelPricingResolver
{
    private static readonly IReadOnlyDictionary<string, ModelPricing> Empty
        = new Dictionary<string, ModelPricing>();

    private readonly IModelPricingResolver _base;
    private readonly IReadOnlyDictionary<string, ModelPricing> _overrides;

    public OverlayModelPricingResolver(IModelPricingResolver baseResolver, PricingConfig? overrides)
    {
        _base = baseResolver;
        _overrides = overrides?.Models is { Count: > 0 } models
            ? new Dictionary<string, ModelPricing>(models, StringComparer.OrdinalIgnoreCase)
            : Empty;
    }

    public ModelPricing? Resolve(string model)
    {
        if (_overrides.TryGetValue(model, out var exact)) return exact;
        var prefix = _overrides
            .Where(kv => model.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Key.Length)
            .Select(kv => kv.Value)
            .FirstOrDefault();
        return prefix ?? _base.Resolve(model);
    }
}
