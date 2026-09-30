using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pricing;

/// <summary>
/// The bundled price list: the embedded snapshot of the public list, plus the committed
/// supplement for ids the list lacks. The snapshot wins wherever both carry an id. Lookup
/// is exact id, then the bare name the refresh script indexed — never a prefix here.
/// </summary>
public sealed class BundledModelPriceList : IBundledModelPriceList
{
    private readonly IReadOnlyDictionary<string, BundledModelPrice> _byId;
    private readonly IReadOnlyDictionary<string, string> _aliases;

    public BundledModelPriceList()
    {
        var reader = new ModelPriceSnapshotReader();
        var snapshot = reader.Read(ModelPriceSnapshotReader.SnapshotResource);
        var supplement = reader.Read(ModelPriceSnapshotReader.SupplementResource);
        Source = snapshot.Source;
        FetchedAt = snapshot.FetchedAt;
        _byId = Merge(snapshot, supplement);
        _aliases = snapshot.Aliases;
        Models = [.. _byId.Values.OrderBy(m => m.Id, StringComparer.Ordinal)];
        PricingByName = BuildPricingByName(_byId, _aliases);
    }

    public string Source { get; }
    public DateTimeOffset FetchedAt { get; }
    public IReadOnlyList<BundledModelPrice> Models { get; }
    public IReadOnlyDictionary<string, ModelPricing> PricingByName { get; }

    public BundledModelPrice? Find(string modelId)
    {
        if (_byId.TryGetValue(modelId, out var exact)) return exact;
        return _aliases.TryGetValue(modelId, out var target) && _byId.TryGetValue(target, out var bare)
            ? bare
            : null;
    }

    private static Dictionary<string, BundledModelPrice> Merge(ModelPriceSnapshot snapshot, ModelPriceSnapshot supplement)
    {
        var merged = new Dictionary<string, BundledModelPrice>(snapshot.Models, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, price) in supplement.Models)
            merged.TryAdd(id, price);
        return merged;
    }

    private static Dictionary<string, ModelPricing> BuildPricingByName(
        IReadOnlyDictionary<string, BundledModelPrice> byId, IReadOnlyDictionary<string, string> aliases)
    {
        var table = byId.ToDictionary(kv => kv.Key, kv => kv.Value.ToPricing(), StringComparer.OrdinalIgnoreCase);
        foreach (var (bare, target) in aliases)
            if (byId.TryGetValue(target, out var price))
                table.TryAdd(bare, price.ToPricing());
        return table;
    }
}
