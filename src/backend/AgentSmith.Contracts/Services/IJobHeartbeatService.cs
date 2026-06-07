using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Publishes a periodic Redis SETEX heartbeat for a running pipeline job.
/// The returned IAsyncDisposable stops renewal and clears the key on dispose —
/// `await using var _ = heartbeat.Start(...)` wraps pipeline execution.
/// </summary>
public interface IJobHeartbeatService
{
    /// <summary>
    /// Starts heartbeat renewal for a running pipeline, tagging the key with
    /// <paramref name="runId"/>. p0238: the value carries the run id so dispose is a
    /// compare-and-delete — a finishing run can never clear a DIFFERENT run's
    /// heartbeat for the same ticket (the old per-ticket clobber that spawned the
    /// run-swarm). The renewal takes over the claim-time bridge key (MarkClaimedAsync).
    /// </summary>
    IAsyncDisposable Start(TicketId ticketId, string runId);

    /// <summary>
    /// p0238: writes a one-shot heartbeat at CLAIM time (before the job is picked
    /// off the queue) so the window between Enqueued and InProgress is covered.
    /// Without it the stale detector / reconciler saw a queued-but-not-started run
    /// as dead and re-spawned it every scan — the swarm. The running job's Start
    /// renewal then keeps it alive; if the job never starts it lapses by TTL.
    /// </summary>
    Task MarkClaimedAsync(TicketId ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns true if a recent heartbeat exists for the ticket. Used by the claim
    /// path (active-run guard), StaleJobDetector and EnqueuedReconciler to decide
    /// whether a ticket is already being processed.
    /// </summary>
    Task<bool> IsAliveAsync(TicketId ticketId, CancellationToken cancellationToken);
}
