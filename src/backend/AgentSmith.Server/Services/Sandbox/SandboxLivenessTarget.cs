namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: addressing tuple passed by PipelineSandboxCoordinator to
/// SandboxLivenessWatcher at start time. Carries the run-id (cancellation
/// scope), the job-id (heartbeat key scope), the container-id (docker
/// inspect target), and the sandbox key (event payload tag).
/// </summary>
public sealed record SandboxLivenessTarget(
    string RunId,
    string JobId,
    string ContainerId,
    string SandboxKey);
