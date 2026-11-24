using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Argo;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-11-23-c81ec: the Argo CD a PoC target's argo block names. In a PoC run the token is token_secret looked up
/// environment first, then the catalog — a catalog value, so the trace masker replaces it; none and an unreadable stored
/// value are refused naming the key. Calls go out on their own HTTP client with redirects off, so the token never
/// follows a redirect.
/// </summary>
public sealed class ArgoCdClientFactory(
    ICredentialResolver credentials, IHttpClientFactory httpClients, TimeProvider clock, ILogger<ArgoCdClientFactory> logger)
    : IArgoCdClientFactory
{
    public const string ClientName = "argo-cd";

    public IArgoCdClient Create(PocArgoConfig argo)
    {
        ArgumentNullException.ThrowIfNull(argo);
        var lookup = credentials.Lookup(argo.TokenSecret);
        return lookup.State switch
        {
            CredentialLookupState.Value => Create(argo, lookup.Value!),
            CredentialLookupState.Unreadable => throw new ArgoCdException($"{PocArgoConfig.Key}.token_secret of Argo CD {argo.Url}: {lookup}"),
            _ => throw new ArgoCdException($"{PocArgoConfig.Key}.token_secret names '{argo.TokenSecret}', which holds no value; "
                + $"set the AppProject role token of Argo CD {argo.Url} there"),
        };
    }

    public IArgoCdClient Create(PocArgoConfig argo, string token)
    {
        ArgumentNullException.ThrowIfNull(argo);
        if (!Uri.TryCreate(argo.Url.TrimEnd('/') + "/", UriKind.Absolute, out var server) || server.Scheme != Uri.UriSchemeHttps)
            throw new ArgoCdException($"{PocArgoConfig.Key}.url is '{argo.Url}'; it must be an https URL, because the role token is sent to it");
        logger.LogDebug("Argo CD {Server} (project {Project})", server.GetLeftPart(UriPartial.Authority), argo.Project);
        return new ArgoCdClient(new ArgoCdHttp(httpClients.CreateClient(ClientName), server, token, logger), argo, token, clock);
    }
}
