namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0348: the cancel grace window as a single shared constant. The CancelEnforcer
/// (kill after grace) and the RunEventApplier (which defaults a kill deadline on
/// the watchdog/projector cancel path so EVERY cancel-requested run becomes an
/// enforcement candidate) must agree on it — before this it lived only on the
/// enforcer and the projector path set no deadline at all, wedging watchdog
/// cancels in "cancelling…" forever.
/// </summary>
public static class CancelPolicy
{
    /// <summary>Grace between a persisted cancel and the force-kill — the window
    /// in which a cooperative (in-process) cancel may land first.</summary>
    public static readonly TimeSpan KillGrace = TimeSpan.FromSeconds(30);
}
