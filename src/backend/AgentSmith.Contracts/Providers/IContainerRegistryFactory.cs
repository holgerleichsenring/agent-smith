using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-933de: the registry a PoC target's registry block names, by its type.</summary>
public interface IContainerRegistryFactory
{
    /// <summary>In a PoC run: the pull token is the value of pull_auth, environment first, then the catalog.</summary>
    IContainerRegistry Create(PocRegistryConfig registry);

    /// <summary>With a pull token the caller resolved — the draft check reads it from the catalog only.</summary>
    IContainerRegistry Create(PocRegistryConfig registry, string pullToken);
}
