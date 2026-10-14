using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-14-dbceg: which tickets landed. Insert only: a landing is written once, by whichever
/// replica's insert wins, and outlives every run of its ticket.
/// </summary>
public interface ITicketLandingStore
{
    /// <summary>Stores the landing and answers true when THIS call inserted it; false when the ticket had landed.</summary>
    Task<bool> TryRecordAsync(TicketLandingFact landing, CancellationToken cancellationToken);

    /// <summary>The stored landings of the named keys on that tracker connection; a key that never landed is absent.</summary>
    Task<IReadOnlyList<TicketLandingFact>> LandedAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-10-14-dbceh: one StartsWhenMet edge per distinct dependent of the named projects that has no landing of
    /// its own and requires a ticket whose landing row was WRITTEN at or after <paramref name="since"/> — newest
    /// landing first, at most <paramref name="take"/> dependents.
    /// </summary>
    Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfLandingsSinceAsync(
        DateTimeOffset since, IReadOnlyCollection<string> projects, int take, CancellationToken cancellationToken);
}
