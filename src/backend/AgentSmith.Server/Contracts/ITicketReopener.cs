using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-10-08-e8b9b: clears what holds a finished ticket from being claimed again — its hand-back
/// state, its approval's SatisfiedAt and a standing unmoved fact. Used by Retry and by the rework
/// entry, AFTER the ticket carries a status a claimer accepts.
/// </summary>
public interface ITicketReopener
{
    Task ClearAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken);
}
