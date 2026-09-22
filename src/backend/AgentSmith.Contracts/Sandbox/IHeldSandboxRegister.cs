namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the holds THIS process may release, and the eviction a capacity
/// door performs before it probes.
/// <para>
/// The register is deliberately process-local while the reapers' rail is not. Reaping
/// a held sandbox wrongly is a correctness failure, so that rail is a shared label and
/// a shared row; failing to evict is only a capacity miss that degrades to the refusal
/// an operator gets today, and the hold lapses on its own inside the window.
/// </para>
/// </summary>
public interface IHeldSandboxRegister
{
    /// <summary>Registers a sandbox as held and released — evictable from this moment.</summary>
    void Hold(HeldSandbox held);

    /// <summary>
    /// A turn takes the hold back. A taken hold is NOT evictable: it is in use, and no
    /// reaper rail could tell it from an idle one, because it still carries the run
    /// label of the turn that spawned it and that run is over.
    /// </summary>
    bool Take(string key);

    /// <summary>The turn ended; the hold is evictable again, from now.</summary>
    void Release(string key);

    /// <summary>
    /// Force-removes every hold this process may release, least-recently-used first,
    /// and returns how many went. Called BEFORE a capacity probe, never after a denial:
    /// a Kubernetes quota's used figure is written by the cluster's own controller and
    /// does not drop when a pod is deleted, so probe-release-reprobe would read the same
    /// stale figure and deny anyway.
    /// </summary>
    Task<int> EvictAsync(CancellationToken cancellationToken);
}
