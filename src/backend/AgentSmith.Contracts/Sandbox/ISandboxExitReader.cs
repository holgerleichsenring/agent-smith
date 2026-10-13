namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: optional marker — the backend can read how its sandbox's main process stands
/// before it is released. Docker and Kubernetes implement it; the in-process backend has no
/// process of its own and does not, so its release carries no evidence.
/// </summary>
public interface ISandboxExitReader
{
    /// <summary>Running, Exited (with code and OOM flag) or Gone; never throws.</summary>
    Task<SandboxExitRead> ReadExitAsync(CancellationToken cancellationToken);
}
