namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-08-25-1806: what a role NAME means at this installation, and which claims a role
/// name is read out of. Application configuration rather than bootstrap: a role mapping
/// changes when a team does, so it lives in the config store, is edited in the Config
/// Studio and applies to the next request.
/// <para>
/// The bootstrap argument that put these four values in <see cref="TokenAuthorityConfig"/>
/// holds for the authority, the audience and the enforce switch — the JwtBearer pipeline is
/// registered before the database exists. It never held here: the catalog that turns a role
/// into permissions is asked once PER REQUEST, so nothing about it precedes the store.
/// </para>
/// </summary>
public sealed class RoleMappingConfig
{
    /// <summary>
    /// The claim carrying role NAMES, read verbatim. Entra emits app roles under
    /// <c>roles</c>; a Keycloak realm nests them under <c>realm_access.roles</c>, which no
    /// flat claim name can address — that installation maps groups instead.
    /// </summary>
    public string RoleClaim { get; set; } = DefaultRoleClaim;

    /// <summary>The claim carrying group values, mapped onto roles by <see cref="GroupRoles"/>.</summary>
    public string GroupClaim { get; set; } = DefaultGroupClaim;

    /// <summary>Group value -> the roles holding it grants. A caller in two mapped groups holds both.</summary>
    public Dictionary<string, List<string>> GroupRoles { get; set; } = [];

    /// <summary>
    /// Roles this installation adds to the built-in three, as bundles over the permission
    /// catalog. Additive: a name that collides with a built-in role does NOT replace it, and
    /// a permission the catalog does not contain is dropped rather than granted.
    /// </summary>
    public Dictionary<string, List<string>> Roles { get; set; } = [];

    /// <summary>
    /// 2026-08-26-7a51: roles an administrator granted HERE, each against the claim it was
    /// written for. Additive beside whatever the directory says, so a person can hold a
    /// directory role and a granted one at once.
    /// </summary>
    public List<PersonGrant> PersonGrants { get; set; } = [];

    /// <summary>
    /// 2026-08-26-7a51: how long an observed caller is kept before the retention service
    /// drops the row. Zero or less keeps them forever.
    /// </summary>
    public int ObservationRetentionDays { get; set; } = DefaultObservationRetentionDays;

    /// <summary>The claim name an installation that has configured nothing reads roles from.</summary>
    public const string DefaultRoleClaim = "roles";

    /// <summary>The claim name an installation that has configured nothing reads groups from.</summary>
    public const string DefaultGroupClaim = "groups";

    /// <summary>Ninety days of observations is a directory's worth of joiners and leavers.</summary>
    public const int DefaultObservationRetentionDays = 90;

    /// <summary>
    /// Whether this mapping says anything an installation would miss. It is what the
    /// migration asks of the bootstrap block: a file that names neither a role, a group
    /// mapping nor a claim of its own has nothing to preserve, so nothing is imported.
    /// </summary>
    public bool IsEmpty =>
        Roles.Count == 0 && GroupRoles.Count == 0
        && RoleClaim == DefaultRoleClaim && GroupClaim == DefaultGroupClaim;

    /// <summary>The mapping an installation's bootstrap block declares, as the store would hold it.</summary>
    public static RoleMappingConfig From(TokenAuthorityConfig auth) => new()
    {
        RoleClaim = auth.RoleClaim,
        GroupClaim = auth.GroupClaim,
        GroupRoles = auth.GroupRoles.ToDictionary(e => e.Key, e => e.Value.ToList()),
        Roles = auth.Roles.ToDictionary(e => e.Key, e => e.Value.ToList()),
    };
}
