namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0201: per-pipeline-run façade the coordinator uses to start one liveness
/// watcher per spawned sandbox. The Server-side implementation knows the
/// Docker client + Redis multiplexer + cancellation registry; the Application
/// layer only sees this thin interface so non-Docker compositions (InProcess
/// tests, future K8s composition) bind a no-op variant.
/// </summary>
public interface ISandboxLivenessSupervisor : IAsyncDisposable
{
    /// <summary>
    /// Begins watching <paramref name="sandbox"/> against <paramref name="runId"/>
    /// and <paramref name="sandboxKey"/>. Does nothing if the sandbox is not a
    /// <see cref="ISandboxLivenessProbeTarget"/> (no backend resource id to probe).
    /// </summary>
    void Watch(string runId, string sandboxKey, ISandbox sandbox);

    /// <summary>
    /// 2026-11-25-9992b: stops every watcher, so a workspace the run replaces on purpose (a restart with one more
    /// toolchain) is not reported vanished; Watch may be called again afterwards.
    /// </summary>
    Task UnwatchAllAsync();
}
