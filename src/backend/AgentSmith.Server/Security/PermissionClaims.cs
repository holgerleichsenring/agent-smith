namespace AgentSmith.Server.Security;

/// <summary>
/// p0503b: where a validated token states what its bearer may do. One claim per
/// permission, carrying a name out of <see cref="Permissions"/> verbatim — the permission
/// is the unit everywhere else, so it is the unit here too.
/// <para>
/// This is the primitive, not the whole story: mapping an authority's own roles, groups
/// and scopes onto these names is *the identity of a caller is decided by its claims*,
/// which is where an operator's org chart meets the catalog. Until then a token states
/// its permissions directly, which is what makes an authority testable at all.
/// </para>
/// </summary>
internal static class PermissionClaims
{
    internal const string Type = "permission";
}
