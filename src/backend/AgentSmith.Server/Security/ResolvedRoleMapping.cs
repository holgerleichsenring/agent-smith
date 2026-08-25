using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: one mapping and the two readers built from it, kept together so a
/// request cannot resolve its role NAMES from one version of the mapping and their
/// PERMISSIONS from another — which is what asking a source twice would allow.
/// </summary>
internal sealed record ResolvedRoleMapping(
    RoleMappingConfig Mapping, RoleCatalog Catalog, CallerRoleReader Reader)
{
    public static ResolvedRoleMapping From(RoleMappingConfig mapping) =>
        new(mapping, new RoleCatalog(mapping), new CallerRoleReader(mapping));
}
