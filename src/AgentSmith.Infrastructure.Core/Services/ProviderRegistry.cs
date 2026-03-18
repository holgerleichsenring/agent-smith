using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Generic dictionary-based registry for ITypedProvider implementations.
/// Resolves providers by their ProviderType key at runtime.
/// </summary>
public sealed class ProviderRegistry<T> where T : ITypedProvider
{
    private readonly Dictionary<string, T> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(IEnumerable<T> providers)
    {
        foreach (var provider in providers)
        {
            if (!_providers.TryAdd(provider.ProviderType, provider))
                throw new ConfigurationException(
                    $"Duplicate provider type '{provider.ProviderType}' registered for {typeof(T).Name}.");
        }
    }

    public T Resolve(string providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
            return provider;

        var available = string.Join(", ", _providers.Keys);
        throw new ConfigurationException(
            $"No {typeof(T).Name} registered for type '{providerType}'. Available: [{available}]");
    }

    public bool TryResolve(string providerType, out T? provider)
    {
        return _providers.TryGetValue(providerType, out provider);
    }

    public IReadOnlyCollection<string> RegisteredTypes => _providers.Keys;
}
