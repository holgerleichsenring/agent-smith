namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-25-b4d9: which loop owns a taken ticket. The reaper (60s) and the reconciler
/// (10min) both act on a ticket whose lease went stale; without this the reconciler could
/// re-launch a ticket the reaper is in the middle of cancelling — two loops over one ticket
/// is how the label thrash of p0260 happened.
/// </summary>
public enum TakenTicketState
{
    /// <summary>The claim was granted and nothing else is acting on the ticket.</summary>
    Taken = 0,

    /// <summary>The reaper holds it: cancelling the run and releasing the lease.</summary>
    Reaping = 1,
}
