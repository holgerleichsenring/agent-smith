using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

namespace AgentSmith.Infrastructure.Extensions;

/// <summary>
/// 2026-10-12-6bfae: the registered builders' price-list providers keyed by every agent type
/// they answer to, aliases included — the shape the capabilities descriptor serves.
/// </summary>
public static class ChatClientBuilderPriceListExtensions
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> PriceListProvidersByType(
        this IEnumerable<IChatClientBuilder> builders)
    {
        var byType = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var builder in builders)
            foreach (var type in builder.SupportedTypes)
                byType[type.ToLowerInvariant()] = builder.PriceListProviders;
        return byType;
    }
}
