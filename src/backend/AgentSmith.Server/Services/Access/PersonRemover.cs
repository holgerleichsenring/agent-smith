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
    RoleMappingSource mapping,
    IObservedCallerStore observed,
    CallerObservationBuffer buffer,
    AccessGrantWriter writer)
{
    /// <summary>
    /// 2026-09-14-2b7c: the in-memory note goes with the two persistent halves. Without it the
    /// row and the grant are gone while the suppression that decides the person's RETURN stays,
    /// so they are absent from the surface and silently dropped on every request they make for
    /// up to five minutes — through any number of sign-ins, because it is keyed by subject.
    /// <para>
    /// Under the buffer's gate, so a batch already in flight cannot write the row back after the
    /// delete: the drain has moved it into a list this code cannot reach, and only excluding the
    /// two operations from each other closes that.
    /// </para>
    /// </summary>
    public async Task<bool> RemoveAsync(string id, ChangeAttribution by, CancellationToken ct)
    {
        var record = (await observed.AllAsync(ct)).FirstOrDefault(c => c.Subject == id);
        // Either identifier can be what the row was keyed by: an observed caller is keyed by
        // its subject, a person named by hand by the value the grant was written against.
        var names = new[] { id, record?.NameValue }.OfType<string>().ToHashSet(StringComparer.Ordinal);
        var removed = await ForgetAndDeleteAsync(id, ct);
        return Withdraw(names, by) || removed;
    }

    private async Task<bool> ForgetAndDeleteAsync(string id, CancellationToken ct)
    {
        using var held = await buffer.HoldAsync(ct);
        buffer.Forget(id);
        return await observed.RemoveAsync(id, ct);
    }

    private bool Withdraw(IReadOnlySet<string> names, ChangeAttribution by)
    {
        var current = mapping.Current().Mapping;
        var kept = current.PersonGrants.Where(grant => !names.Contains(grant.Value)).ToList();
        var bindings = Withdrawn(current.ProjectBindings, names);
        if (kept.Count == current.PersonGrants.Count && Members(bindings) == Members(current.ProjectBindings))
            return false;
        writer.Save(Without(current, kept, bindings), by);
        return true;
    }

    // 2026-10-15-4a4df: a ref left behind would hand a returning namesake the project rights an
    // administrator thought were removed. A binding THIS withdrawal empties goes; one saved
    // memberless stays, its members still to come.
    private static List<ProjectBinding> Withdrawn(List<ProjectBinding> bindings, IReadOnlySet<string> names)
    {
        var kept = new List<ProjectBinding>();
        foreach (var binding in bindings)
        {
            var people = binding.People.Where(p => !names.Contains(p.Value)).ToList();
            if (people.Count < binding.People.Count && people.Count == 0 && binding.Groups.Count == 0) continue;
            kept.Add(new ProjectBinding
            {
                Project = binding.Project, Groups = [.. binding.Groups], Roles = [.. binding.Roles],
                People = [.. people.Select(p => new PersonRef { Claim = p.Claim, Value = p.Value })],
            });
        }

        return kept;
    }

    private static int Members(List<ProjectBinding> bindings) => bindings.Sum(binding => binding.People.Count);

    // A copy, never the cached instance: the source hands the same object to every request
    // and treats its identity as the "nothing changed" signal. Every field is named, because
    // one this copy leaves out is erased by the save.
    private static RoleMappingConfig Without(
        RoleMappingConfig current, List<PersonGrant> kept, List<ProjectBinding> bindings) => new()
    {
        RoleClaim = current.RoleClaim,
        GroupClaim = current.GroupClaim,
        GroupRoles = current.GroupRoles.ToDictionary(e => e.Key, e => e.Value.ToList()),
        Roles = current.Roles.ToDictionary(e => e.Key, e => e.Value.ToList()),
        PersonGrants = kept,
        ProjectBindings = bindings,
        ObservationRetentionDays = current.ObservationRetentionDays,
    };
}
