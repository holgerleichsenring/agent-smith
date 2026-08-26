using AgentSmith.Contracts.Models.Access;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the whole access surface in one read — people, groups, roles and the
/// claim names. Four panes over ONE document plus the observed callers, assembled here
/// rather than by four routes, so no two panes can disagree about the same save.
/// </summary>
internal sealed class AccessViewComposer(AccessPeopleComposer people)
{
    private const string OpaqueSubjectClaim = "sub";

    public AccessView Compose(ResolvedRoleMapping current, IReadOnlyList<ObservedCaller> observed)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(observed);
        var rows = people.Compose(current, observed);
        var groups = Groups(current, observed);
        return new AccessView(
            current.Mapping.RoleClaim, current.Mapping.GroupClaim, current.NameClaim,
            current.Mapping,
            !string.Equals(current.NameClaim, OpaqueSubjectClaim, StringComparison.Ordinal),
            current.Mapping.ObservationRetentionDays,
            rows, groups, Roles(current, rows, groups),
            Security.Permissions.All,
            [.. current.Catalog.Findings.Concat(current.Persons.Findings)]);
    }

    private static IReadOnlyList<AccessGroupView> Groups(
        ResolvedRoleMapping current, IReadOnlyList<ObservedCaller> observed)
    {
        var carried = observed.SelectMany(caller => caller.GroupValues)
            .GroupBy(Normalize, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var mapped = current.Mapping.GroupRoles.ToDictionary(
            e => Normalize(e.Key), e => (IReadOnlyList<string>)e.Value, StringComparer.Ordinal);
        return [.. carried.Keys.Concat(mapped.Keys).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(value => new AccessGroupView(
                value, mapped.GetValueOrDefault(value) ?? [], carried.GetValueOrDefault(value)))];
    }

    private static IReadOnlyList<AccessRoleView> Roles(
        ResolvedRoleMapping current,
        IReadOnlyList<AccessPersonView> people,
        IReadOnlyList<AccessGroupView> groups) =>
        [.. current.Catalog.Names.Select(name => new AccessRoleView(
            name,
            BuiltInRoles.All.Keys.Contains(name, StringComparer.OrdinalIgnoreCase),
            current.Catalog.Permissions([name]),
            people.Count(person => Holds(person, name)),
            groups.Count(group => group.Roles.Contains(name, StringComparer.OrdinalIgnoreCase))))];

    private static bool Holds(AccessPersonView person, string role) =>
        person.GrantedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
        || person.DirectoryRoles.Any(origin =>
            string.Equals(origin.Role, role, StringComparison.OrdinalIgnoreCase));

    // A Keycloak group path is "/platform-admins"; the value an operator copies out of the
    // console is "platform-admins". One leading slash, and only that.
    private static string Normalize(string value) => value.TrimStart('/');
}
