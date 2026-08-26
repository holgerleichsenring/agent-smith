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
/// <para>
/// 2026-08-25-1806: the four mapping fields below are now the migration SEED rather than
/// what runs. <see cref="RoleMappingConfig"/> in the config store is what resolves a role,
/// and an installation whose mapping is still in its file has it imported once. They stay
/// bound here because a file that has not migrated must keep being read — dropping them
/// would lock out everyone the mapping let in.
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
    /// 2026-08-25-1806: the seed for <see cref="RoleMappingConfig.RoleClaim"/>. Entra emits
    /// app roles under <c>roles</c>; a Keycloak realm nests them under
    /// <c>realm_access.roles</c>, which no flat claim name can address — that installation
    /// maps groups instead.
    /// </summary>
    public string RoleClaim { get; init; } = RoleMappingConfig.DefaultRoleClaim;

    /// <summary>2026-08-25-1806: the seed for <see cref="RoleMappingConfig.GroupClaim"/>.</summary>
    public string GroupClaim { get; init; } = RoleMappingConfig.DefaultGroupClaim;

    /// <summary>
    /// The claim a caller is NAMED by — on the identity page and in a config change's
    /// attribution. <c>sub</c> is opaque but always present; an installation that wants a
    /// readable name points this at <c>preferred_username</c> or <c>upn</c>.
    /// </summary>
    public string NameClaim { get; init; } = "sub";

    /// <summary>2026-08-25-1806: the seed for <see cref="RoleMappingConfig.GroupRoles"/>.</summary>
    public Dictionary<string, List<string>> GroupRoles { get; init; } = [];

    /// <summary>2026-08-25-1806: the seed for <see cref="RoleMappingConfig.Roles"/>.</summary>
    public Dictionary<string, List<string>> Roles { get; init; } = [];

    /// <summary>An authority is the one thing without which nothing else here can work.</summary>
    public bool IsUsable => !string.IsNullOrWhiteSpace(Authority);
}
