namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Marker for providers that self-identify by type string.
/// Used by ProviderRegistry for dictionary-style lookup.
/// </summary>
public interface ITypedProvider
{
    string ProviderType { get; }
}
