namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>2026-11-23-933de: what a HEAD on a manifest found.</summary>
public enum OciManifestState
{
    Found,
    Missing,

    /// <summary>The token was issued, but the registry answered 401 or 403 for this repository.</summary>
    Denied,

    /// <summary>The token endpoint refused the pull credentials.</summary>
    TokenRefused,
}
