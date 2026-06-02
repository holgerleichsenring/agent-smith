namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0201: optional marker that a sandbox implementation exposes a backend
/// resource the SandboxLivenessWatcher can probe (today: Docker container id).
/// PipelineSandboxCoordinator checks for this interface before starting a
/// watcher — non-Docker sandboxes (InProcess, Kubernetes) simply don't
/// implement it and run without a watcher.
/// </summary>
public interface ISandboxLivenessProbeTarget
{
    /// <summary>Opaque backend resource id (Docker container id, K8s pod uid, …).</summary>
    string LivenessProbeTargetId { get; }
}
