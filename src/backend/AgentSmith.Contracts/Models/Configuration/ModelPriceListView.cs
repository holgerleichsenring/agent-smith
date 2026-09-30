namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The bundled price list as <c>GET /api/config/model-prices</c> serves it: where it
/// came from, when it was taken, and every model it prices.
/// </summary>
public sealed record ModelPriceListView(
    string Source, DateTimeOffset FetchedAt, IReadOnlyList<BundledModelPrice> Models);
