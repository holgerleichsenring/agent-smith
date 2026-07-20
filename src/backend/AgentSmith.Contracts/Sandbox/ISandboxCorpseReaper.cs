namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0355: deletes CORPSE sandbox pods — pods whose owning run is no longer live.
/// A sandbox pod is normally torn down when its <see cref="ISandbox"/> disposes,
/// but a crashed or restarted replica never runs that dispose, so the pod lingers
/// and keeps holding the namespace ResourceQuota — later runs are then admitted and
/// immediately killed with "exceeded quota". This sweep lists the agentsmith
/// sandbox pods by label, maps each pod's run-id to whether a live run owns it, and
/// deletes the corpses — independent of the DB run row (a corpse dies even when its
/// run row is gone). Invoked BOTH on a periodic timer (leader-elected housekeeping)
/// and at capacity-claim time (reconcile-then-admit). The default composition binds
/// a no-op; only the Kubernetes backend reaps real pods.
/// </summary>
public interface ISandboxCorpseReaper
{
    /// <summary>
    /// Delete every sandbox pod whose run-id label maps to no live run. Returns the
    /// number of pods reaped. Never throws — a backend read failure logs and reaps
    /// nothing, so a probe outage never blocks admission.
    /// </summary>
    Task<int> ReapCorpsesAsync(CancellationToken cancellationToken);
}
