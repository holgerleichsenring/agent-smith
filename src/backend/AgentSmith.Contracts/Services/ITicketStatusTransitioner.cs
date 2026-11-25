using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Transitions a ticket between lifecycle statuses, guarded where the platform's
/// write accepts a precondition (the Azure DevOps rev) and last-write-wins where it
/// accepts none; Jira is additionally serialised by a Redis lock in the server.
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
