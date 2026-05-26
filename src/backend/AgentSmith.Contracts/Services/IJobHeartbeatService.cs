using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Publishes a periodic Redis SETEX heartbeat for a running pipeline job.
/// The returned IAsyncDisposable stops renewal and clears the key on dispose —
/// `await using var _ = heartbeat.Start(...)` wraps pipeline execution.
/// </summary>
public interface IJobHeartbeatService
{
    IAsyncDisposable Start(TicketId ticketId);

    /// <summary>
    /// Returns true if a recent heartbeat exists for the ticket. Used by StaleJobDetector
    /// and EnqueuedReconciler to decide whether a ticket is actively being processed.
    /// </summary>
    Task<bool> IsAliveAsync(TicketId ticketId, CancellationToken cancellationToken);
}
