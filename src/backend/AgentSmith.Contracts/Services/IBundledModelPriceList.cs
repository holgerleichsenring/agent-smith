using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// The public model price list, bundled into the binary as a snapshot and refreshed
/// as a release step (tools/update-model-prices.py) — never fetched at runtime. It is
/// the base every price lookup starts from; an agent's <c>pricing:</c> table overrides it.
/// </summary>
public interface IBundledModelPriceList
{
    /// <summary>Where the snapshot was taken from.</summary>
    string Source { get; }

    /// <summary>When the snapshot was taken, UTC.</summary>
    DateTimeOffset FetchedAt { get; }

    /// <summary>Every listed model, the committed supplement included, ordered by id.</summary>
    IReadOnlyList<BundledModelPrice> Models { get; }

    /// <summary>
    /// Every name a lookup may use — model ids and unambiguous bare names of
    /// provider-prefixed ids — to its pricing, ignoring case. The runtime resolver
    /// walks it for its longest-prefix fallback.
    /// </summary>
    IReadOnlyDictionary<string, ModelPricing> PricingByName { get; }

    /// <summary>
    /// The strict lookup: the exact id, then the bare name of a provider-prefixed id,
    /// ignoring case — never a prefix, so a typo that starts with a real id is not found.
    /// </summary>
    BundledModelPrice? Find(string modelId);
}
