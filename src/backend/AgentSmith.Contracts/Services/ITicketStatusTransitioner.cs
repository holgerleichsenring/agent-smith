using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Atomically transitions a ticket between lifecycle statuses using the
/// strongest concurrency primitive the platform offers (ETag, rev, or lock).
/// </summary>
public interface ITicketStatusTransitioner : ITypedProvider
{
    Task<TransitionResult> TransitionAsync(
        TicketId ticketId,
        TicketLifecycleStatus from,
        TicketLifecycleStatus to,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the current lifecycle status from the ticket (by inspecting labels/tags/native status).
    /// Returns null if no lifecycle label is present (treat as Pending from the claim-service perspective).
    /// </summary>
    Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId,
        CancellationToken cancellationToken);
}
