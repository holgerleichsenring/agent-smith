using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-13-a72a: decides whether the slices this ticket follows have left the working
/// set. Asked as the FIRST statement of the spawn funnel, before anything is validated,
/// sized, reserved or enqueued — a blocked child must leave no state behind, and the
/// capacity queue's pump claims its head DIRECTLY rather than through the funnel, so a
/// gate placed after the defer branch would be bypassed by a second, ungated door.
/// </summary>
public interface IPredecessorGate
{
    Task<PredecessorVerdict> CheckAsync(
        ResolvedProject project, IncomingTicketEnvelope envelope, CancellationToken cancellationToken);
}
