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

    /// <summary>
    /// DELETE the lease at normal run-end (or rollback) so the ticket is reclaimable.
    /// p0459: the release NAMES its holder — the row is only deleted when it belongs to
    /// <paramref name="runId"/>, so a run that a newer one reclaimed cannot strip the newer
    /// run's claim. Pass null for a claim no run has taken up yet (the claim region rolling
    /// its own claim back, a run cancelled before it started): null matches an UNATTACHED
    /// row only. A release that does not match is a no-op reported as HeldByAnotherRun.
    /// </summary>
    Task<LeaseReleaseOutcome> ReleaseAsync(
        string project, TicketId ticketId, string? runId, CancellationToken cancellationToken);

    /// <summary>Attach the run id (+ orchestrator job handle) once the run starts; renews the heartbeat.</summary>
    Task AttachRunAsync(
        string project, TicketId ticketId, string runId, string? jobId, CancellationToken cancellationToken);

    /// <summary>Renew the lease heartbeat (liveness only — NOT the claim guard).</summary>
    Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>Leases whose heartbeat is older than the threshold — reaper candidates.</summary>
    Task<IReadOnlyList<StaleLease>> FindStaleAsync(TimeSpan olderThan, CancellationToken cancellationToken);

    /// <summary>
    /// p0242: the lease for one ticket (with its attached run id), or null when no
    /// run is active. The stale-revert path reads this to CANCEL the run it reverts.
    /// </summary>
    Task<StaleLease?> GetByTicketAsync(string project, TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// p0242: run ids of leases whose heartbeat is still fresh (live runs). The
    /// sandbox-orphan reaper unions this flush-proof DB set with the volatile Redis
    /// active-set, so an empty/flushed Redis cannot make live sandboxes look dead.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetActiveRunIdsAsync(TimeSpan freshFor, CancellationToken cancellationToken);
}
