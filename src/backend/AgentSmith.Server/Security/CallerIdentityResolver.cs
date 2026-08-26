using System.Security.Claims;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503d: the one place a token becomes a set of permissions. Roles come from the
/// directory (a role claim, or a group this installation maps), the catalog says what a
/// role means, and the environment grant unions an administrator in whatever either says.
/// The <c>permission</c> claims p0503b reads straight off a token stay in the union, so an
/// authority that states permissions directly keeps working.
/// <para>
/// 2026-08-25-1806: the mapping is asked for per call rather than captured at startup, so a
/// role bundle saved in the Config Studio governs the very next request.
/// </para>
/// </summary>
internal sealed class CallerIdentityResolver(RoleMappingSource mapping, AdminGrant grant)
{
    private const string SubjectClaim = "sub";

    public CallerIdentity Resolve(ClaimsPrincipal caller)
    {
        var current = mapping.Current();
        if (caller.Identity?.IsAuthenticated != true) return Anonymous(current);

        var groups = current.Reader.GroupClaimValues(caller);
        var held = Held(current, caller, groups);
        return new CallerIdentity(
            Authenticated: true,
            Subject: caller.Identity.Name ?? caller.FindFirst(SubjectClaim)?.Value,
            Issuer: caller.FindFirst("iss")?.Value ?? caller.Claims.FirstOrDefault()?.Issuer,
            current.Mapping.RoleClaim, current.Mapping.GroupClaim,
            current.Reader.RoleClaimValues(caller), groups,
            held, Permissions(current, caller, held),
            [.. current.Catalog.Findings.Concat(grant.Findings).Concat(GroupOverageDetector.Findings(caller))]);
    }

    private IReadOnlyList<string> Held(
        ResolvedRoleMapping current, ClaimsPrincipal caller, IReadOnlyList<string> groups)
    {
        var held = current.Reader.Roles(caller);
        return grant.Holds(groups, caller.FindFirst(SubjectClaim)?.Value)
            ? [.. held.Append(BuiltInRoles.Admin).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)]
            : held;
    }

    /// <summary>
    /// <c>identity.read</c> is unioned in for every authenticated caller, which is the one
    /// exception to "a permission comes from a role". The identity page exists for the
    /// caller who holds NOTHING yet; refusing them it would refuse the only surface that
    /// says why they hold nothing, and the refusal would be a 403 naming a permission they
    /// cannot see the catalog of.
    /// </summary>
    private static IReadOnlyList<string> Permissions(
        ResolvedRoleMapping current, ClaimsPrincipal caller, IReadOnlyList<string> held) =>
        [.. current.Catalog.Permissions(held)
            .Concat(caller.FindAll(PermissionClaims.Type).Select(claim => claim.Value))
            .Append(Security.Permissions.IdentityRead)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    // A caller who presented nothing and one whose token was refused both arrive here as
    // the same anonymous principal. 2026-08-25-1806 tells them apart on the ANONYMOUS
    // requirements route instead of this one, because an enforcing installation refuses
    // this route to exactly the caller who needs the answer.
    private static CallerIdentity Anonymous(ResolvedRoleMapping current) => new(
        Authenticated: false, Subject: null, Issuer: null,
        current.Mapping.RoleClaim, current.Mapping.GroupClaim, [], [], [], [],
        current.Catalog.Findings);
}
