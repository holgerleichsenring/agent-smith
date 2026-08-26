using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the one action that removes a person — their grant AND the record of
/// having seen them.
/// <para>
/// The two are different kinds of thing: a grant is a decision that travels in a config
/// export, an observation is not configuration at all and must never reach another
/// installation. Saying "records are not exported" would be a true sentence hiding a false
/// impression, so removal is offered once and does both.
/// </para>
/// </summary>
internal sealed class PersonRemover(
    RoleMappingSource mapping, IObservedCallerStore observed, AccessGrantWriter writer)
{
    public async Task<bool> RemoveAsync(string id, ChangeAttribution by, CancellationToken ct)
    {
        var record = (await observed.AllAsync(ct)).FirstOrDefault(c => c.Subject == id);
        // Either identifier can be what the row was keyed by: an observed caller is keyed by
        // its subject, a person named by hand by the value the grant was written against.
        var names = new[] { id, record?.NameValue }.OfType<string>().ToHashSet(StringComparer.Ordinal);
        var removed = await observed.RemoveAsync(id, ct);
        return Withdraw(names, by) || removed;
    }

    private bool Withdraw(IReadOnlySet<string> names, ChangeAttribution by)
    {
        var current = mapping.Current().Mapping;
        var kept = current.PersonGrants.Where(grant => !names.Contains(grant.Value)).ToList();
        if (kept.Count == current.PersonGrants.Count) return false;
        writer.Save(Without(current, kept), by);
        return true;
    }

    // A copy, never the cached instance: the source hands the same object to every request
    // and treats its identity as the "nothing changed" signal.
    private static RoleMappingConfig Without(RoleMappingConfig current, List<PersonGrant> kept) => new()
    {
        RoleClaim = current.RoleClaim,
        GroupClaim = current.GroupClaim,
        GroupRoles = current.GroupRoles.ToDictionary(e => e.Key, e => e.Value.ToList()),
        Roles = current.Roles.ToDictionary(e => e.Key, e => e.Value.ToList()),
        PersonGrants = kept,
        ObservationRetentionDays = current.ObservationRetentionDays,
    };
}
