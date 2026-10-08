namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-08-e8b9e: what attaching a run to a ticket's lease found. Attached is the zero value on
/// purpose: a composition with no lease to speak of answers it by default.
/// </summary>
public enum LeaseAttachOutcome
{
    /// <summary>The row was unattached, this run's own, stale, or absent and inserted.</summary>
    Attached = 0,

    /// <summary>Another run with a fresh heartbeat holds the lease; nothing was written.</summary>
    HeldByAnotherRun = 1,
}
