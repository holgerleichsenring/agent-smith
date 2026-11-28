namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the pointer half of the split persistence. 2026-11-28-db88a: the row also stores the ticket's working spec
/// set — the set as last published, which a run and every host reader take as the ticket's set. The spec files on the
/// branch are the written record, never the source the set is read from.
/// <para>
/// The two halves are written apart on purpose: <see cref="SaveAsync"/> never writes the set and <see cref="GetAsync"/>
/// never loads it, so a read-modify-write of the pointer (a reopen, a hand-back counter) cannot write an older set back.
/// </para>
/// </summary>
public interface ISpecSetPointerStore
{
    Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken cancellationToken);

    Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken cancellationToken);

    /// <summary>2026-11-28-db88a: the stored set of the ticket, or null when its row stores none — no row, or a row
    /// written before sets were stored (a ticket in flight at that deploy).</summary>
    Task<SpecSet?> GetSetAsync(string project, string key, CancellationToken cancellationToken);

    /// <summary>2026-11-28-db88a: stores <paramref name="set"/> as the ticket's set, creating the row when there is none.
    /// With <paramref name="onlyIfAbsent"/> a set already stored is kept — the one-time import of a ticket in flight never
    /// replaces a set a run stored meanwhile.</summary>
    Task SaveSetAsync(string project, SpecSet set, CancellationToken cancellationToken, bool onlyIfAbsent = false);
}
