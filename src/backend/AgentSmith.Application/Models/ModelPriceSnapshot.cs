using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// One parsed price-list file: its models by id and the bare names the refresh script
/// indexed for provider-prefixed ids (bare name to the id it stands for). The committed
/// supplement carries no source, date or aliases.
/// </summary>
public sealed record ModelPriceSnapshot(
    string Source,
    DateTimeOffset FetchedAt,
    IReadOnlyDictionary<string, BundledModelPrice> Models,
    IReadOnlyDictionary<string, string> Aliases);
