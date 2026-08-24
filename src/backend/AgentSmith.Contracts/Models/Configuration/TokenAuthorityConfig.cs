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
/// <para>
/// p0503d: the directory says which ROLES a caller holds — through a role claim, or
/// through a group membership this block maps onto a role. Nothing here assigns a role to
/// a person; that happens at the identity provider, and there is no screen for it.
/// </para>
/// </summary>
public sealed class TokenAuthorityConfig
{
    /// <summary>The OIDC issuer. Empty means no authentication is registered at all.</summary>
    public string? Authority { get; init; }

    /// <summary>The audience a token must carry. Empty means the audience is not checked.</summary>
    public string? Audience { get; init; }

    public bool Enforce { get; init; }

    /// <summary>
    /// The claim carrying role NAMES, read verbatim. Entra emits app roles under
    /// <c>roles</c>; a Keycloak realm nests them under <c>realm_access.roles</c>, which no
    /// flat claim name can address — that installation maps groups instead.
    /// </summary>
    public string RoleClaim { get; init; } = "roles";

    /// <summary>The claim carrying group values, mapped onto roles by <see cref="GroupRoles"/>.</summary>
    public string GroupClaim { get; init; } = "groups";

    /// <summary>
    /// The claim a caller is NAMED by — on the identity page and in a config change's
    /// attribution. <c>sub</c> is opaque but always present; an installation that wants a
    /// readable name points this at <c>preferred_username</c> or <c>upn</c>.
    /// </summary>
    public string NameClaim { get; init; } = "sub";

    /// <summary>Group value -> the roles holding it grants. A caller in two mapped groups holds both.</summary>
    public Dictionary<string, List<string>> GroupRoles { get; init; } = [];

    /// <summary>
    /// Roles this installation adds to the built-in three, as bundles over the catalog.
    /// Additive: a name that collides with a built-in role does NOT replace it, and a
    /// permission the catalog does not contain is dropped rather than granted.
    /// </summary>
    public Dictionary<string, List<string>> Roles { get; init; } = [];

    /// <summary>An authority is the one thing without which nothing else here can work.</summary>
    public bool IsUsable => !string.IsNullOrWhiteSpace(Authority);
}
