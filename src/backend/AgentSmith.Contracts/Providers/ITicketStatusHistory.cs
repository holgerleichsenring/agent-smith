using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-08-2123: the newest change of a ticket into one of <paramref name="statuses"/> made by a
/// person — neither the token's own identity (<paramref name="self"/>) nor an app or bot. GitHub and
/// GitLab report a reopen, which counts only when "open" or "opened" is one of the statuses.
/// </summary>
public interface ITicketStatusHistory
{
    Task<TicketStatusMove?> NewestPersonMoveIntoAsync(
        TicketId ticketId, IReadOnlyCollection<string> statuses, TrackerActor? self, CancellationToken cancellationToken);
}
