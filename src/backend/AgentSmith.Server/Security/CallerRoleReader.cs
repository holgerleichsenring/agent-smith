using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503d: which roles a token says its bearer holds. Two routes, both configured by claim
/// NAME: a role claim carrying role names verbatim, and a group claim whose values this
/// installation maps onto roles. A directory that nests its roles — Keycloak puts realm
/// roles under <c>realm_access.roles</c>, which arrives as one claim whose value is JSON
/// text — cannot be addressed by a flat name, and takes the group route instead.
/// <para>
/// The comparisons are pinned per surface. Role names fold case, because a directory
/// decides the capitalisation and an operator cannot. Group values compare ORDINALLY: an
/// Entra group value is an opaque object identifier, and case-folding an opaque identifier
/// is a smell. A Keycloak group PATH arrives with a leading slash that the console does not
/// show, so that one character is normalised away on both sides.
/// </para>
/// </summary>
internal sealed class CallerRoleReader
{
    private readonly TokenAuthorityConfig _auth;
    private readonly Dictionary<string, List<string>> _groupRoles = new(StringComparer.Ordinal);

    public CallerRoleReader(TokenAuthorityConfig auth)
    {
        _auth = auth;
        foreach (var (group, roles) in auth.GroupRoles) _groupRoles[Normalize(group)] = roles;
    }

    /// <summary>The role-claim values verbatim, so the identity page can show what arrived.</summary>
    public IReadOnlyList<string> RoleClaimValues(ClaimsPrincipal caller) =>
        Values(caller, _auth.RoleClaim);

    /// <summary>The group-claim values verbatim, unmapped ones included.</summary>
    public IReadOnlyList<string> GroupClaimValues(ClaimsPrincipal caller) =>
        Values(caller, _auth.GroupClaim);

    /// <summary>Role names from the role claim, unioned with the roles the groups map onto.</summary>
    public IReadOnlyList<string> Roles(ClaimsPrincipal caller) =>
        [.. RoleClaimValues(caller)
            .Concat(GroupClaimValues(caller).SelectMany(Mapped))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];

    private IEnumerable<string> Mapped(string group) =>
        _groupRoles.TryGetValue(Normalize(group), out var roles) ? roles : [];

    private static IReadOnlyList<string> Values(ClaimsPrincipal caller, string claim) =>
        [.. caller.FindAll(claim).Select(c => c.Value).Where(v => v.Length > 0)];

    // A Keycloak group path is "/platform-admins"; the key an operator copies out of the
    // console is "platform-admins". One leading slash, and only that.
    private static string Normalize(string group) => group.TrimStart('/');
}
