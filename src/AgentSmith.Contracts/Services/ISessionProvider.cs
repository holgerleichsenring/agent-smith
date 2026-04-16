using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Detects login flow from swagger (Bearer/Cookie/Basic) and performs login requests
/// to obtain bearer tokens for each configured persona.
/// </summary>
public interface ISessionProvider
{
    /// <summary>
    /// Authenticates the given persona against the target API and returns a bearer token.
    /// Returns null if authentication fails.
    /// </summary>
    Task<string?> AuthenticateAsync(
        string targetBaseUrl,
        SwaggerSpec spec,
        PersonaCredentials credentials,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to re-authenticate a persona after a 401 response.
    /// Returns the new token, or null if re-auth fails.
    /// </summary>
    Task<string?> ReAuthenticateAsync(
        string targetBaseUrl,
        SwaggerSpec spec,
        PersonaCredentials credentials,
        CancellationToken cancellationToken)
        => AuthenticateAsync(targetBaseUrl, spec, credentials, cancellationToken);
}
