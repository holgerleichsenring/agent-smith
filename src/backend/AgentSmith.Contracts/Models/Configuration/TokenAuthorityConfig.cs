namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0503b: the ONE authority a presented token is validated against, plus the switch that
/// decides whether the permissions p0503a declared on every route are allowed to refuse
/// anything. Bootstrap-only — it is read from the config file and the environment before
/// the database exists, and it is deliberately absent from the config store, so no export
/// can emit a block the installation does not have.
/// <para>
/// <see cref="Enforce"/> is the gate, NOT the presence of an authority: with an authority
/// configured and the switch off, tokens are still validated but nothing is refused. That
/// is what keeps an installation reachable while its operator prepares an authority and
/// before the dashboard has a way to sign in.
/// </para>
/// </summary>
public sealed class TokenAuthorityConfig
{
    /// <summary>The OIDC issuer. Empty means no authentication is registered at all.</summary>
    public string? Authority { get; init; }

    /// <summary>The audience a token must carry. Empty means the audience is not checked.</summary>
    public string? Audience { get; init; }

    public bool Enforce { get; init; }

    /// <summary>An authority is the one thing without which nothing else here can work.</summary>
    public bool IsUsable => !string.IsNullOrWhiteSpace(Authority);
}
