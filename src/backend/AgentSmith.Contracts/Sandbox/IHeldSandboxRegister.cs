namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the holds THIS process may release, and the eviction a capacity
/// door performs before it probes. 2026-09-22-2d11b: and what a design turn takes back,
/// so only the first message of a conversation pays a spawn and a clone.
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
    /// A turn takes the hold back, verified through the agent's heartbeat: a live sandbox,
    /// or nothing. Nothing means the turn spawns as it always did — a held sandbox may have
    /// been reaped once its window lapsed, evicted under capacity pressure, or lost with its
    /// daemon, and one whose heartbeat has gone is dropped and force-removed here.
    /// <para>
    /// A taken hold LEAVES the register, so an eviction running beside a turn can neither
    /// see it nor pull it out from under the read in flight; the turn puts it back with
    /// <see cref="Hold"/> when it ends.
    /// </para>
    /// </summary>
    Task<IHoldableSandbox?> TakeAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Force-removes every hold this process may release, least-recently-used first,
    /// and returns how many went. Called BEFORE a capacity probe, never after a denial:
    /// a Kubernetes quota's used figure is written by the cluster's own controller and
    /// does not drop when a pod is deleted, so probe-release-reprobe would read the same
    /// stale figure and deny anyway.
    /// </summary>
    Task<int> EvictAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 2026-09-22-2d11b: a hold ends when its conversation does — closed, forked away from
    /// or deleted. The entries leave the register before the first await, so a message that
    /// arrives mid-release can never take one back; the removals themselves are what the
    /// caller may walk away from, and every caller does.
    /// </summary>
    Task ReleaseConversationAsync(string conversationId, CancellationToken cancellationToken);
}
