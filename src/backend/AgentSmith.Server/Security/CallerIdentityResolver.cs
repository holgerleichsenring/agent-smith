using System.Security.Claims;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Server.Contracts;
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
/// <para>
/// 2026-08-26-7a51: a role an administrator granted a PERSON unions in beside the
/// directory's, and the caller is noted so the next grant is picked rather than typed.
/// Noting is best-effort in both directions: it happens after the identity is built, and a
/// caller is never refused because their observation could not be taken.
/// </para>
/// </summary>
internal sealed class CallerIdentityResolver(
    RoleMappingSource mapping,
    AdminGrant grant,
    ICallerObservations observations,
    TimeProvider clock,
    ILogger<CallerIdentityResolver> logger)
{
    private const string SubjectClaim = "sub";

    public CallerIdentity Resolve(ClaimsPrincipal caller)
    {
        var current = mapping.Current();
        if (caller.Identity?.IsAuthenticated != true) return Anonymous(current);

        var groups = current.Reader.GroupClaimValues(caller);
        var held = Held(current, caller, groups);
        var roleValues = current.Reader.RoleClaimValues(caller);
        Note(current, caller, roleValues, groups);
        return new CallerIdentity(
            Authenticated: true,
            Subject: caller.Identity.Name ?? caller.FindFirst(SubjectClaim)?.Value,
            Issuer: caller.FindFirst("iss")?.Value ?? caller.Claims.FirstOrDefault()?.Issuer,
            current.Mapping.RoleClaim, current.Mapping.GroupClaim,
            roleValues, groups,
            held, Permissions(current, caller, held),
            [.. current.Catalog.Findings.Concat(grant.Findings).Concat(current.Persons.Findings)
                .Concat(GroupOverageDetector.Findings(caller))]);
    }

    private IReadOnlyList<string> Held(
        ResolvedRoleMapping current, ClaimsPrincipal caller, IReadOnlyList<string> groups)
    {
        var held = current.Reader.Roles(caller).Concat(current.Persons.Roles(caller));
        if (grant.Holds(groups, caller.FindFirst(SubjectClaim)?.Value))
            held = held.Append(BuiltInRoles.Admin);
        return [.. held.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)];
    }

    // The subject is the real 'sub' and never the name-claim value: a grant is written
    // against one of the two and an environment grant matches the other, so a record that
    // stored only one could not say which of them it held.
    private void Note(
        ResolvedRoleMapping current, ClaimsPrincipal caller,
        IReadOnlyList<string> roleValues, IReadOnlyList<string> groups)
    {
        var subject = caller.FindFirst(SubjectClaim)?.Value ?? caller.Identity?.Name;
        if (string.IsNullOrEmpty(subject)) return;
        var now = clock.GetUtcNow();
        try
        {
            observations.Observe(new ObservedCaller(
                subject, current.NameClaim, caller.Identity?.Name ?? subject,
                roleValues, groups, GroupOverageDetector.GroupsWereOmitted(caller), now, now));
        }
        catch (Exception ex)
        {
            // An observation nobody could take is a surface that lists one caller fewer,
            // never a caller who is refused.
            logger.LogWarning(ex, "The caller could not be noted for the access surface");
        }
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
