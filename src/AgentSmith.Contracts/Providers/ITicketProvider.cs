using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;


/// <summary>
/// Provides access to tickets from an external system (Azure DevOps, Jira, GitHub).
/// </summary>
public interface ITicketProvider : ITypedProvider
{
    Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all open tickets in the configured project.
    /// Returns an empty list if the provider does not support listing.
    /// </summary>
    Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Ticket>>(Array.Empty<Ticket>());

    /// <summary>
    /// Creates a new ticket with the given title and description.
    /// Returns the ID of the newly created ticket.
    /// </summary>
    Task<int> CreateAsync(string title, string description, CancellationToken cancellationToken)
        => throw new NotSupportedException($"CreateAsync is not supported by {nameof(ITicketProvider)}");

    /// <summary>
    /// Creates a new ticket with the given title, description, and labels.
    /// Returns the ID of the newly created ticket.
    /// </summary>
    Task<int> CreateAsync(string title, string description, IReadOnlyList<string> labels, CancellationToken cancellationToken)
        => CreateAsync(title, description, cancellationToken);

    /// <summary>
    /// Posts a status comment to the ticket.
    /// </summary>
    Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Closes the ticket with a resolution comment.
    /// </summary>
    Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Transitions the ticket to the named status (e.g. "In Review").
    /// No-op if the provider does not support transitions.
    /// </summary>
    Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Lists tickets whose lifecycle label matches the given status. Used by
    /// EnqueuedReconciler and StaleJobDetector to enumerate Enqueued/InProgress tickets.
    /// Default: empty list — providers that don't support lifecycle search don't participate.
    /// </summary>
    Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Ticket>>(Array.Empty<Ticket>());

    /// <summary>
    /// Returns attachment references found on the ticket.
    /// Default: empty list (providers that have no attachments skip this).
    /// </summary>
    Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AttachmentRef>>(Array.Empty<AttachmentRef>());

    /// <summary>
    /// Downloads image attachments from the ticket, returning ready-to-use image objects.
    /// Default: empty list. Providers override to handle platform-specific auth.
    /// </summary>
    Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TicketImageAttachment>>([]);
}
