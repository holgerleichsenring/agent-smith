using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-15-114fa: which tickets a person closed. One mark per ticket: the newest close overwrites it, a Reopen
/// deletes it.
/// </summary>
public interface ITicketClosedMarkStore
{
    /// <summary>Stores the mark, overwriting the ticket's earlier one — last writer wins.</summary>
    Task SetAsync(TicketClosedMark mark, CancellationToken cancellationToken);

    /// <summary>Deletes the ticket's mark; true when a mark was there.</summary>
    Task<bool> ClearAsync(string tracker, string ticketKey, CancellationToken cancellationToken);

    /// <summary>The marks of the named keys on that tracker connection; a key never closed is absent.</summary>
    Task<IReadOnlyList<TicketClosedMark>> OfAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken);
}
