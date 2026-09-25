using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-25-b4d9: the durable record of the tickets this framework has taken up. One row
/// per (project, ticket), written by the claim and cleared by completion — never by a timer,
/// because a record deleted on a timer is the same disappearing act one layer down.
/// </summary>
public interface ITakenTicketStore
{
    /// <summary>Records a granted claim, in <see cref="TakenTicketState.Taken"/>.</summary>
    Task TakeAsync(TakenTicketFact fact, CancellationToken cancellationToken);

    /// <summary>Drops the record — the ticket finished, or the claim rolled itself back.</summary>
    Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// Every record no other loop is holding: the reconciler's candidates, found without
    /// asking any tracker for a lifecycle label.
    /// </summary>
    Task<IReadOnlyList<TakenTicketFact>> ListReconcilableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks the record as being reaped. False ONLY when it is already marked (another pass
    /// holds it); a ticket with no record is nobody's, so it is true and the reap proceeds.
    /// </summary>
    Task<bool> TryBeginReapAsync(string project, string ticketId, CancellationToken cancellationToken);

    /// <summary>Hands the ticket back: the reap is over and the record is reconcilable again.</summary>
    Task EndReapAsync(string project, string ticketId, CancellationToken cancellationToken);
}
