using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-26-7a51: whether a role mapping still leaves somebody a way to reach
/// <see cref="BuiltInRoles.Admin"/>.
/// <para>
/// Four routes count, and any one is enough: a person granted admin here, a group mapped
/// onto admin, a role CLAIM this installation reads (the directory can put <c>admin</c>
/// in it and no server can see that from here), or an environment grant that parses to
/// somebody. Written the other way round — "refuse removing the last admin" — it would
/// count the wrong thing, because an installation whose administrators arrive through the
/// role claim holds no grants at all while clearing the claim name still empties them.
/// </para>
/// </summary>
internal sealed class AdminRoute(AdminGrant grant)
{
    internal const string Refusal =
        "This would leave the installation with no way to reach the admin role: no person "
        + "is granted it, no group maps onto it, no role claim is read, and "
        + AdminGrant.EnvVar + " names nobody. Keep one of the four before saving.";

    public bool ExistsIn(RoleMappingConfig mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return Granted(mapping) || Mapped(mapping)
            || !string.IsNullOrWhiteSpace(mapping.RoleClaim) || grant.NamesSomebody;
    }

    private static bool Granted(RoleMappingConfig mapping) =>
        mapping.PersonGrants.Any(g => Holds(g.Roles));

    private static bool Mapped(RoleMappingConfig mapping) =>
        mapping.GroupRoles.Values.Any(Holds);

    // Role names fold case everywhere else in this server, so 'Admin' is 'admin' here too.
    private static bool Holds(IEnumerable<string> roles) =>
        roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase);
}
