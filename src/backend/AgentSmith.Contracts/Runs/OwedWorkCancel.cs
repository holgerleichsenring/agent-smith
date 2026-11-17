namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-17-166aa: the cancel reasons that mean the PROCESS was lost, not the work — the liveness reaper and
/// the queued-run sweeper (interrupted) and the stale-lease release (its owning replica went away). The work is
/// still owed, so such an end neither holds the ticket nor reads as a person's or the run's own stop. One place for
/// the two literals that the reapers write and the readers of an end compare against.
/// </summary>
public static class OwedWorkCancel
{
    public const string Interrupted = "interrupted";
    public const string StaleLeaseReaped = "stale-lease-reaped";

    public static bool IsOwed(string? cancelReason) => cancelReason is Interrupted or StaleLeaseReaped;
}
