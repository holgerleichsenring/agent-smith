using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-18-c1a7: the standing record of a ticket whose status a run could not move.
/// One row per (project, ticket) — the fact answers "will the next claim fail the same
/// way", not "what happened", so a second failure overwrites the first.
/// </summary>
public interface IUnmovedTicketStore
{
    /// <summary>
    /// Records the fact, stamped with the configuration it was recorded under: the versions
    /// of the tracker and project documents that named the status the tracker refused.
    /// </summary>
    Task RecordAsync(UnmovedTicketFact fact, CancellationToken cancellationToken);

    /// <summary>
    /// The record that still stands, or null — because none was written, or because the
    /// tracker's or the project's configuration document has changed since it was, which is
    /// exactly what an operator does to fix it.
    /// </summary>
    Task<UnmovedTicketFact?> FindStandingAsync(
        string project, string ticketId, string tracker, CancellationToken cancellationToken);

    /// <summary>Drops the record for one ticket — the operator's retry, which re-triggers it.</summary>
    Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken);
}
