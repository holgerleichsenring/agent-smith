using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;


/// <summary>
/// Provides access to tickets from an external system (Azure DevOps, Jira, GitHub).
/// </summary>
public interface ITicketProvider : ITypedProvider
{
    /// <summary>
    /// p0140a: declares whether this provider's backing system supports comments on tickets.
    /// Default true — all of today's providers (Jira/ADO/GitHub/GitLab) have in-band comments.
    /// The future Email provider (p0141) returns false. Webhook handlers will check this
    /// before calling <see cref="UpdateStatusAsync"/> from p0140b's zero-match / capability-
    /// conditional paths.
    /// </summary>
    bool SupportsComments => true;

    /// <summary>
    /// Read-only connectivity probe: performs the cheapest authenticated round-trip
    /// the tracker supports and reports whether the credentials work and the remote
    /// is reachable. Never writes. Implementations must not throw — transport/auth
    /// failures are captured in <see cref="ConnectionProbeResult.Error"/>.
    /// </summary>
    Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken);

    Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all open tickets in the configured project.
    /// Returns an empty list if the provider does not support listing.
    /// </summary>
    Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Ticket>>(Array.Empty<Ticket>());

    /// <summary>
    /// Lists the tickets a composed <see cref="DiscoveryQuery"/> selects as CLAIMABLE —
    /// per routed project, native status ∈ trigger_statuses AND the resolution criterion,
    /// OR'd — so the poller fetches only candidates instead of every open ticket. Default
    /// delegates to <see cref="ListOpenAsync"/>: providers that can't push the query
    /// server-side (GitHub/GitLab today) stay broad and rely on the in-process filter.
    /// </summary>
    Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
        => ListOpenAsync(cancellationToken);

    /// <summary>
    /// Creates a new ticket with the given title, description and labels/tags
    /// and returns its id plus web URL. Deliberately NOT a default member:
    /// creation must never silently no-op, so a provider that cannot create
    /// has to state it in code (throw <see cref="NotSupportedException"/>)
    /// instead of inheriting a throwing default nobody implemented (the
    /// pre-p0315f state — the create path was dead on every tracker).
    /// </summary>
    Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels,
        CancellationToken cancellationToken);

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
    /// Post-PR finalize: in one provider-native step, post the summary comment
    /// AND move the ticket to <paramref name="doneStatus"/> (or close it when
    /// <paramref name="doneStatus"/> is null/empty).
    /// </summary>
    /// <remarks>
    /// On Azure DevOps the two changes MUST land in the same WIT PATCH —
    /// AzDO bumps <c>System.Rev</c> after every write and any concurrent
    /// observer (other agent-smith run, operator UI edit, server-side
    /// automation rule) between two sequential PATCHes produces
    /// <c>TF26071: This work item has been changed by someone else</c>
    /// and the second call aborts. Other providers (GitHub/GitLab/Jira)
    /// have no equivalent rev guard, so the default body's two sequential
    /// calls are safe — they implement this method by delegation.
    /// </remarks>
    Task FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken);

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
