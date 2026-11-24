using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-c81ec: the Argo CD a PoC target's argo block names.</summary>
public interface IArgoCdClientFactory
{
    /// <summary>In a PoC run: the token is the value of token_secret, environment first, then the catalog.</summary>
    IArgoCdClient Create(PocArgoConfig argo);

    /// <summary>With a token the caller resolved — the draft check reads it from the catalog only.</summary>
    IArgoCdClient Create(PocArgoConfig argo, string token);
}
