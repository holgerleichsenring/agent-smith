namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-25-c1f7: the approved records of one tracker connection that no run has satisfied
/// yet, as a discovery query can carry them — the TRACKER'S OWN ticket ids, oldest approval
/// first, plus how many the cap left out.
/// <para>
/// The omitted COUNT is part of the answer rather than a detail of the query: the set is
/// bounded because a JQL/WIQL clause is, and a ticket that did not fit is one the poll will not
/// find until an older record is satisfied. Reported, it is a line an operator can act on;
/// dropped in silence it is a ticket that simply never runs.
/// </para>
/// </summary>
/// <param name="TicketIds">The tracker's own ids, oldest approval first, at most the asked limit.</param>
/// <param name="Omitted">How many further unsatisfied records the limit excluded.</param>
public sealed record OutstandingApprovals(IReadOnlyList<string> TicketIds, int Omitted)
{
    /// <summary>Nothing outstanding — also the honest answer of a store that cannot enumerate.</summary>
    public static OutstandingApprovals None { get; } = new([], 0);
}
