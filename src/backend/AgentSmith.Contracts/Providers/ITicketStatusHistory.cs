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

    /// <summary>
    /// 2026-10-13-9a59k: the newest move into <paramref name="status"/> by ANY actor — the token's own
    /// and apps included, <see cref="TrackerActor.IsApp"/> carried — so a caller can tell whose close
    /// it was. GitHub and GitLab know "closed" (a close) and "open"/"opened" (a reopen) only.
    /// </summary>
    Task<TicketStatusMove?> NewestMoveIntoAsync(TicketId ticketId, string status, CancellationToken cancellationToken);
}
