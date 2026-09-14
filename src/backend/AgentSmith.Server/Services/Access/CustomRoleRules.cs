using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-09-14-91ad: what a custom role may be, checked where the person who wrote it is
/// still looking at it.
/// <para>
/// This replaces 2026-08-26-7a51's NewCustomRoleGuard, which refused every new custom role.
/// That was right while the surface had no way to compose one and the only case was a
/// legacy bundle to round-trip; it stopped being right once the answer to "may start a run,
/// may not delete one" was a config import — the YAML this product took away.
/// </para>
/// <para>
/// Scoped to what this save ADDS OR CHANGES. The access route takes the whole document on
/// every PUT, so a check over all of it would refuse a person grant in the People pane
/// because of a legacy role nobody touched — and that role is exactly the thing
/// <see cref="RoleCatalog"/> tolerates with a finding. Untouched is untouched.
/// </para>
/// </summary>
internal sealed class CustomRoleRules
{
    public void Against(RoleMappingConfig inForce, RoleMappingConfig incoming)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        ArgumentNullException.ThrowIfNull(incoming);
        foreach (var (name, bundle) in incoming.Roles)
        {
            if (Unchanged(inForce, name, bundle)) continue;
            RefuseBuiltInName(name);
            RefuseUnknownPermissions(name, bundle);
        }
    }

    // Role NAMES fold case, the way every role lookup in this server does.
    private static void RefuseBuiltInName(string name)
    {
        if (!BuiltInRoles.All.Keys.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
        throw new ConfigurationException(
            $"'{name}' is a built-in role and cannot be redefined here. A custom role is additive: "
            + $"pick a name outside {string.Join(", ", BuiltInRoles.All.Keys)} and it stands beside "
            + "them.");
    }

    // Permission names do NOT fold: the catalog compares them ordinally, so a save that
    // accepted 'Runs.Read' would hand the catalog a name it then drops — which is the
    // silent drop this phase exists to end, arriving through the door meant to stop it.
    private static void RefuseUnknownPermissions(string name, IReadOnlyList<string> bundle)
    {
        var unknown = bundle
            .Where(p => !Permissions.All.Contains(p, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (unknown.Count == 0) return;
        throw new ConfigurationException(
            $"The role '{name}' names {(unknown.Count == 1 ? "a permission" : "permissions")} this "
            + $"installation does not have: {string.Join(", ", unknown)}. Names are case-sensitive, "
            + "and the catalog the surface offers is the whole of what can be granted.");
    }

    private static bool Unchanged(
        RoleMappingConfig inForce, string name, IReadOnlyList<string> bundle) =>
        inForce.Roles.TryGetValue(name, out var before)
        && before.SequenceEqual(bundle, StringComparer.Ordinal);
}
