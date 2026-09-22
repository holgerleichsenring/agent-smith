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
    /// Whether the backing system has in-band comments on tickets. True for Jira, Azure DevOps,
    /// GitHub and GitLab; a provider without them returns false.
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
    /// Creates a new ticket with the given title, description and labels/tags and returns its
    /// id plus web URL. <paramref name="kind"/> is the tracker's own work-item type for the role
    /// being filed; null means the literal the provider sends today, and a tracker with no such
    /// notion ignores it. Deliberately NOT a default member: creation must never silently no-op,
    /// so a provider that cannot create has to state it in code (throw
    /// <see cref="NotSupportedException"/>) instead of inheriting a throwing default nobody implemented.
    /// </summary>
    Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Links a created child ticket to its parent through the tracker's own relation. No default,
    /// for the reason <see cref="CreateAsync"/> has none. A relation the tracker lacks is
    /// Unsupported, and one it refuses is Failed with its reason — neither throws.
    /// </summary>
    Task<ParentLinkResult> LinkToParentAsync(
        CreatedTicket child, TicketId parent, CancellationToken cancellationToken);

    /// <summary>Posts a status comment to the ticket.</summary>
    Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>Closes the ticket with a resolution comment and answers whether it CLOSED (9519).</summary>
    Task<bool> CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
        => Task.FromResult(false);

    /// <summary>Moves the ticket to the named status and answers whether it moved. Default: false.</summary>
    Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        => Task.FromResult(false);

    /// <summary>
    /// 2026-09-20-2ba8: adds ONE label to a ticket that already exists, and answers whether the
    /// label is now on it. The label travels WHOLE — a provider that joins labels into one
    /// delimited field must not split it, and a caller offering a value that tracker's grammar
    /// cannot carry is the caller's bug to prevent. Default: false, the way the port's other
    /// later members no-op, so a caller reports a tag it never wrote as NOT applied.
    /// </summary>
    Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken cancellationToken)
        => Task.FromResult(false);

    /// <summary>
    /// Post-PR finalize: in one provider-native step, post the summary comment
    /// AND move the ticket to <paramref name="doneStatus"/> (or close it when
    /// <paramref name="doneStatus"/> is null/empty). Reports whether the status
    /// actually MOVED — a value the tracker refuses, offers no transition to, or
    /// this provider cannot express is not a success, and a caller that reads it
    /// as one leaves the ticket in the status the next poll claims again.
    /// </summary>
    /// <remarks>
    /// On Azure DevOps the two changes MUST land in the same WIT PATCH — AzDO bumps
    /// <c>System.Rev</c> after every write and any concurrent observer (other
    /// agent-smith run, operator UI edit, server-side automation rule) between two
    /// sequential PATCHes produces <c>TF26071: This work item has been changed by
    /// someone else</c> and the second call aborts. Other providers have no
    /// equivalent rev guard, so two sequential calls are safe there.
    /// </remarks>
    Task<TicketFinalizeResult> FinalizeAsync(
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
    /// p0317: reads the ticket's comment thread (the conversation) in the tracker's
    /// natural order. Default: empty list — providers whose backing system has no
    /// in-band comments (<see cref="SupportsComments"/> false) simply never override.
    /// Comment bodies are ticket-origin text: callers treat them as UNTRUSTED input.
    /// </summary>
    Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TicketComment>>([]);

    /// <summary>Returns attachment references found on the ticket. Default: empty list
    /// (providers that have no attachments skip this).</summary>
    Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AttachmentRef>>(Array.Empty<AttachmentRef>());

    /// <summary>Downloads image attachments from the ticket, returning ready-to-use image
    /// objects. Default: empty list. Providers override to handle platform-specific auth.</summary>
    Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TicketImageAttachment>>([]);

    /// <summary>
    /// p0317: downloads the ticket's text-like document attachments (txt/md/pdf/docx
    /// within the size cap). Other binaries are never downloaded — the caller lists
    /// them by name + size only. Default: empty list.
    /// </summary>
    Task<IReadOnlyList<TicketDocumentAttachment>> DownloadDocumentAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TicketDocumentAttachment>>([]);
}
