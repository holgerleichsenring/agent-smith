using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// The single-run lease backed by the ActiveRun UNIQUE(Project,TicketId) index.
/// The CONSTRAINT is the guard: TryClaim is an INSERT that the index rejects for
/// a duplicate. The heartbeat is liveness only (the reaper's input, not the
/// guard). CLI mode binds a no-op implementation — claim/lease is a server-only
/// concern.
/// </summary>
public interface IActiveRunLease
{
    /// <summary>INSERT the lease; the unique index maps a duplicate to AlreadyClaimed.</summary>
    Task<LeaseClaimOutcome> TryClaimAsync(string project, TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>DELETE the lease at normal run-end (or rollback) so the ticket is reclaimable.</summary>
    Task ReleaseAsync(string project, TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>Attach the run id (+ orchestrator job handle) once the run starts; renews the heartbeat.</summary>
    Task AttachRunAsync(
        string project, TicketId ticketId, string runId, string? jobId, CancellationToken cancellationToken);

    /// <summary>Renew the lease heartbeat (liveness only — NOT the claim guard).</summary>
    Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>Leases whose heartbeat is older than the threshold — reaper candidates.</summary>
    Task<IReadOnlyList<StaleLease>> FindStaleAsync(TimeSpan olderThan, CancellationToken cancellationToken);
}
