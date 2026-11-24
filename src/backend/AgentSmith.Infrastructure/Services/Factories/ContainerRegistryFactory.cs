using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Registry;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-11-23-933de: the container registry a PoC target's registry block names. In a PoC run the pull token is
/// pull_auth looked up environment first, then the catalog; none and an unreadable stored value are refused naming the
/// key or the reason. Calls go out on their own HTTP client with redirects off, so no credential follows a redirect.
/// </summary>
public sealed class ContainerRegistryFactory(
    ICredentialResolver credentials, IHttpClientFactory httpClients, ILogger<ContainerRegistryFactory> logger)
    : IContainerRegistryFactory
{
    public const string ClientName = "container-registry";

    public IContainerRegistry Create(PocRegistryConfig registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var lookup = credentials.Lookup(registry.PullAuth);
        return lookup.State switch
        {
            CredentialLookupState.Value => Create(registry, lookup.Value!),
            CredentialLookupState.Unreadable => throw new ContainerRegistryException(
                $"{PocRegistryConfig.Key}.pull_auth of registry {registry.LoginServer}: {lookup}"),
            _ => throw new ContainerRegistryException($"{PocRegistryConfig.Key}.pull_auth names '{registry.PullAuth}', "
                + $"which holds no value; set the pull-only token of registry {registry.LoginServer} there"),
        };
    }

    public IContainerRegistry Create(PocRegistryConfig registry, string pullToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        logger.LogDebug("Container registry {LoginServer} ({Type})", registry.LoginServer, registry.Type);
        return registry.Type switch
        {
            PocRegistryConfig.Acr => new AcrContainerRegistry(registry, Reader(registry, pullToken)),
            _ => throw new ContainerRegistryException($"{PocRegistryConfig.Key}.type is '{registry.Type}'; "
                + $"agent smith reads '{PocRegistryConfig.Acr}' registries only so far"),
        };
    }

    private OciRegistryReader Reader(PocRegistryConfig registry, string pullToken) =>
        new(new OciDistributionClient(httpClients.CreateClient(ClientName), registry.LoginServer, registry.PullUser,
            pullToken, logger), registry.Repository);
}
