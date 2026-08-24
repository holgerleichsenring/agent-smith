using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503d: the one place a token becomes a set of permissions. Roles come from the
/// directory (a role claim, or a group this installation maps), the catalog says what a
/// role means, and the environment grant unions an administrator in whatever either says.
/// The <c>permission</c> claims p0503b reads straight off a token stay in the union, so an
/// authority that states permissions directly keeps working.
/// </summary>
internal sealed class CallerIdentityResolver(
    TokenAuthorityConfig auth, CallerRoleReader roles, RoleCatalog catalog, AdminGrant grant)
{
    private const string SubjectClaim = "sub";

    public CallerIdentity Resolve(ClaimsPrincipal caller)
    {
        if (caller.Identity?.IsAuthenticated != true) return Anonymous();

        var groups = roles.GroupClaimValues(caller);
        var held = Held(caller, groups);
        return new CallerIdentity(
            Authenticated: true,
            Subject: caller.Identity.Name ?? caller.FindFirst(SubjectClaim)?.Value,
            Issuer: caller.FindFirst("iss")?.Value ?? caller.Claims.FirstOrDefault()?.Issuer,
            auth.RoleClaim, auth.GroupClaim,
            roles.RoleClaimValues(caller), groups,
            held, Permissions(caller, held),
            [.. catalog.Findings.Concat(grant.Findings).Concat(GroupOverageDetector.Findings(caller))]);
    }

    private IReadOnlyList<string> Held(ClaimsPrincipal caller, IReadOnlyList<string> groups)
    {
        var held = roles.Roles(caller);
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
    private IReadOnlyList<string> Permissions(ClaimsPrincipal caller, IReadOnlyList<string> held) =>
        [.. catalog.Permissions(held)
            .Concat(caller.FindAll(PermissionClaims.Type).Select(claim => claim.Value))
            .Append(Security.Permissions.IdentityRead)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private CallerIdentity Anonymous() => new(
        Authenticated: false, Subject: null, Issuer: null,
        auth.RoleClaim, auth.GroupClaim, [], [], [], [], catalog.Findings);
}
