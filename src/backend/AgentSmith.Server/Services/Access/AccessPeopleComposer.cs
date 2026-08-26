using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the People pane's rows — everyone this installation has observed,
/// plus everyone an administrator named by hand who has not called yet.
/// <para>
/// A hand-named person is a row with no timestamp rather than an absence: "not signed in
/// yet" and "signed in and holds nothing" are different situations, and the administrator
/// who just typed the value is the one who most needs to see which they are looking at.
/// </para>
/// </summary>
internal sealed class AccessPeopleComposer
{
    public IReadOnlyList<AccessPersonView> Compose(
        ResolvedRoleMapping current, IReadOnlyList<ObservedCaller> observed)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(observed);
        var seen = observed.Select(caller => Seen(current, caller)).ToList();
        var named = seen.Select(person => person.NameValue).ToHashSet(StringComparer.Ordinal);
        // Never-seen first: they are the rows somebody just typed, and the observed ones
        // arrive newest-first behind them.
        return [.. current.Mapping.PersonGrants
            .Where(grant => !named.Contains(grant.Value))
            .Select(Unseen)
            .Concat(seen)];
    }

    private static AccessPersonView Seen(ResolvedRoleMapping current, ObservedCaller caller) => new(
        caller.Subject, caller.Subject, caller.NameClaim, caller.NameValue,
        Directory(current, caller), current.Persons.Roles(caller.NameValue),
        caller.GroupValues, caller.GroupsOmitted, caller.FirstSeen, caller.LastSeen);

    // A grant whose claim is no longer the name claim still gets a row: it is reported as a
    // finding, and a row nobody can see is a row nobody can withdraw.
    private static AccessPersonView Unseen(PersonGrant grant) => new(
        grant.Value, Subject: null, grant.Claim, grant.Value,
        [], [.. grant.Roles.Order(StringComparer.Ordinal)], [], GroupsOmitted: false,
        FirstSeen: null, LastSeen: null);

    private static IReadOnlyList<AccessRoleOriginView> Directory(
        ResolvedRoleMapping current, ObservedCaller caller) =>
        [.. caller.RoleValues.Select(role => new AccessRoleOriginView(role, current.Mapping.RoleClaim))
            .Concat(caller.GroupValues.SelectMany(group => Mapped(current, group)))
            .DistinctBy(origin => (origin.Role, origin.Via), TupleComparer)];

    private static IEnumerable<AccessRoleOriginView> Mapped(ResolvedRoleMapping current, string group) =>
        current.Mapping.GroupRoles
            // A Keycloak group PATH arrives with a leading slash the console does not show.
            .Where(entry => entry.Key.TrimStart('/') == group.TrimStart('/'))
            .SelectMany(entry => entry.Value)
            .Select(role => new AccessRoleOriginView(role, current.Mapping.GroupClaim));

    private static readonly IEqualityComparer<(string Role, string Via)> TupleComparer =
        EqualityComparer<(string Role, string Via)>.Default;
}
