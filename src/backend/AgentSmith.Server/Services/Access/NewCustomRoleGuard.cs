using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: custom roles became READ-ONLY. They came out of p0503d's catalog
/// rather than a request, and they make the surface harder to read for a case nobody has.
/// <para>
/// Deleting the data would drop everyone holding one to zero permissions, so an existing
/// custom role is rendered, round-tripped verbatim and reported — and a NEW one is refused
/// here, at the save, rather than being quietly dropped afterwards.
/// </para>
/// </summary>
internal sealed class NewCustomRoleGuard
{
    public void Against(RoleMappingConfig inForce, RoleMappingConfig incoming)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        ArgumentNullException.ThrowIfNull(incoming);
        var added = incoming.Roles.Keys
            .Where(name => !inForce.Roles.ContainsKey(name))
            .Where(name => !BuiltInRoles.All.Keys.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (added.Count > 0) throw new ConfigurationException(Refusal(added));
    }

    private static string Refusal(IReadOnlyList<string> added) =>
        $"A new custom role cannot be added here: {string.Join(", ", added.Order(StringComparer.Ordinal))}. "
        + $"The roles this installation offers are {string.Join(", ", BuiltInRoles.All.Keys)}, plus any "
        + "custom role it already had, which keeps working and is rendered read-only.";
}
