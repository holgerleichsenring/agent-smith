namespace AgentSmith.Contracts.Models;

/// <summary>
/// Outcome of a TicketClaimService.ClaimAsync call.
/// </summary>
public enum ClaimOutcome
{
    Claimed,
    AlreadyClaimed,
    Rejected,
    Failed,

    /// <summary>
    /// p0269a: the run was NOT claimed because the sandbox capacity (k8s
    /// ResourceQuota / Docker concurrent-sandbox cap) is exhausted right now. The
    /// ticket is left in its trigger status so the next poll retries — this is how
    /// tickets that don't fit together are processed sequentially. Distinct from
    /// Failed: nothing went wrong, the run is waiting for room.
    /// </summary>
    Queued
}
