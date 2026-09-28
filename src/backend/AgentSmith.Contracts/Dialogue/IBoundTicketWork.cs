using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// 2026-09-28-1da5c: what the tracker shows against THIS conversation's ticket.
/// <para>
/// The ticket is not a parameter — it is the one the turn was seeded with, exactly as the ticket
/// read and the withdrawal are. A design turn asked about "the ticket's branches" means its own.
/// </para>
/// </summary>
public interface IBoundTicketWork
{
    Task<TicketLinkedWorkResult> ForAsync(CancellationToken cancellationToken);
}
