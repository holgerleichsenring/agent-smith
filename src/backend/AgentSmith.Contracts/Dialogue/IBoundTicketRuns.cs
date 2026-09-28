using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// 2026-09-28-1da5d: the runs this framework made against THIS conversation's ticket.
/// <para>
/// The record exists — runs are stored by project and ticket, with their phases and the pull
/// requests they opened — and a design turn could not reach it: the reader that composes it is a
/// PAGE read addressed by dialog id, and a turn's seeds carried no run record at all.
/// </para>
/// </summary>
public interface IBoundTicketRuns
{
    Task<TicketRunsResult> ForAsync(CancellationToken cancellationToken);
}
