namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0269a: answers "will a sandbox of this footprint fit right now?" BEFORE a run
/// is claimed, so a full namespace/host queues the next run instead of spawning,
/// being rejected, and terminal-failing. Provider-agnostic so no k8s/Docker type
/// leaks into the Application core (feedback_spawner_model):
/// <list type="bullet">
///   <item>Kubernetes reads the namespace ResourceQuota (hard vs used).</item>
///   <item>Docker counts labelled sandbox containers against a configured cap
///   (an unlimited daemon gives no create-time signal, so the cap is the guard).</item>
///   <item>InProcess / CLI / test composition admits unconditionally.</item>
/// </list>
/// The probe is ADVISORY: the atomic guard stays the spawn itself (a TOCTOU race
/// between probe and create is real and handled by the typed capacity rejection
/// on the spawn path). Both routes reach the same "queued" outcome.
/// </summary>
public interface ISandboxCapacityProbe
{
    /// <summary>
    /// True when a sandbox requesting <paramref name="footprint"/> can be created now.
    /// Implementations must not throw on a transient backend read failure — they
    /// admit (fail-open) so a probe outage never blocks all runs; the spawn-path
    /// capacity rejection remains the hard guard.
    /// </summary>
    Task<CapacityDecision> HasCapacityAsync(ResourceLimits footprint, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a capacity probe. <see cref="Admitted"/> true means "go"; false carries
/// a human <see cref="Reason"/> (which resource is full) for the waiting signal.
/// </summary>
public sealed record CapacityDecision(bool Admitted, string? Reason = null)
{
    public static CapacityDecision Admit() => new(true);
    public static CapacityDecision Deny(string reason) => new(false, reason);
}
