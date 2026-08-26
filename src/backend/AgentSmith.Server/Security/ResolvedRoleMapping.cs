using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: one mapping and the readers built from it, kept together so a request
/// cannot resolve its role NAMES from one version of the mapping and their PERMISSIONS
/// from another — which is what asking a source twice would allow.
/// <para>
/// 2026-08-26-7a51: the name claim rides along because a person grant resolves against it,
/// and it is bootstrap while the mapping is not — pairing them here is what stops a grant
/// being matched against a claim nobody configured.
/// </para>
/// </summary>
internal sealed record ResolvedRoleMapping(
    RoleMappingConfig Mapping, string NameClaim,
    RoleCatalog Catalog, CallerRoleReader Reader, PersonGrantReader Persons)
{
    public static ResolvedRoleMapping From(RoleMappingConfig mapping, string nameClaim) =>
        new(mapping, nameClaim, new RoleCatalog(mapping), new CallerRoleReader(mapping),
            new PersonGrantReader(mapping, nameClaim));
}
