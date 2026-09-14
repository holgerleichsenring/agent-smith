using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-09-14-91ad: removing a custom role drops everyone holding it to zero permissions,
/// with no surface anywhere saying why. So it is refused while a holder can still be seen,
/// and the refusal names them.
/// <para>
/// The list is a FLOOR, not a census. Three kinds of holder are visible — granted here,
/// through a mapped group, and observed arriving with the role in the directory's claim —
/// and each of the three can be cleared: withdraw the grant, unmap the group, or withdraw
/// the role in the directory and forget the person in the People pane. A holder who never
/// signed in and holds it only through that claim cannot be seen at all, which is the same
/// blind spot AdminRoute states about admin.
/// </para>
/// <para>
/// A store that cannot be READ refuses the removal rather than permitting it. The tempting
/// shortcut, asking through AccessSurfaceReader, fails open — it catches and returns an
/// empty list — so it would permit the destructive act precisely when the holders are
/// invisible.
/// </para>
/// </summary>
internal sealed class RoleRemovalGuard(IObservedCallerStore observed)
{
    public async Task AgainstAsync(
        RoleMappingConfig inForce, RoleMappingConfig incoming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        ArgumentNullException.ThrowIfNull(incoming);
        var removed = inForce.Roles.Keys.Where(name => !incoming.Roles.ContainsKey(name)).ToList();
        if (removed.Count == 0) return;

        var seen = await SeenAsync(cancellationToken);
        foreach (var role in removed.Order(StringComparer.Ordinal))
        {
            // Judged against the document that is about to EXIST, so withdrawing the role and
            // removing it in one save is one deliberate act rather than two refused ones.
            var holders = Holders(incoming, seen, role);
            if (holders.Count > 0) throw new ConfigurationException(Refusal(role, holders));
        }
    }

    private static IReadOnlyList<string> Holders(
        RoleMappingConfig incoming, IReadOnlyList<ObservedCaller> seen, string role)
    {
        var holders = new List<string>();
        holders.AddRange(incoming.PersonGrants
            .Where(g => Holds(g.Roles, role))
            .Select(g => $"{g.Value} (granted here)"));
        holders.AddRange(incoming.GroupRoles
            .Where(pair => Holds(pair.Value, role))
            .Select(pair => $"{pair.Key} (mapped group)"));
        holders.AddRange(seen
            .Where(c => Holds(c.RoleValues, role))
            .Select(c => $"{c.NameValue} (seen carrying it from the directory)"));
        return [.. holders.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static string Refusal(string role, IReadOnlyList<string> holders) =>
        $"The role '{role}' is still held by {string.Join(", ", holders)}, and removing it would "
        + "leave them with no permissions at all and nothing saying why. Withdraw it from each of "
        + "them first — a grant here, a group mapping, or the role in your directory, after which "
        + "the People pane can forget the person. This list names the holders this installation "
        + "can SEE; somebody who holds it through the directory and has not signed in lately is "
        + "not in it.";

    private async Task<IReadOnlyList<ObservedCaller>> SeenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await observed.AllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ConfigurationException(
                "Removing a role needs the list of who holds it, and the observed-caller store "
                + $"could not be read: {ex.Message}. The removal is refused rather than carried "
                + "out blind.");
        }
    }

    private static bool Holds(IEnumerable<string> roles, string role) =>
        roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
