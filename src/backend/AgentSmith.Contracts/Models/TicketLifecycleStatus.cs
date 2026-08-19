namespace AgentSmith.Contracts.Models;

/// <summary>
/// Lifecycle state of a ticket managed by the claim-then-enqueue flow.
/// The status is persisted on the ticket itself (via labels/tags/native statuses)
/// and is the source of truth — Redis only holds the queue and heartbeats.
/// </summary>
public enum TicketLifecycleStatus
{
    Pending,
    Enqueued,
    InProgress,
    Done,
    Failed,

    /// <summary>
    /// p0454: the run parked on a person and is waiting for them. Distinct from Done:
    /// on the board a run waiting for a human and a run that finished were the same row.
    /// </summary>
    Waiting
}
