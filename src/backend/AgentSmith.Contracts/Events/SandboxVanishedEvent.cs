namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0201: published by SandboxLivenessWatcher when the sandbox container is
/// confirmed gone (heartbeat missing for &gt;3 ticks AND docker-inspect either
/// returns 404 or reports a non-Running state). Lands on the run stream so the
/// trail carries the verdict; <see cref="ContainerState"/> distinguishes
/// "Exited(137)" (OOM kill) from "Gone" (docker daemon lost the container) so
/// the operator doesn't have to docker-log dive.
/// </summary>
public sealed record SandboxVanishedEvent(
    string RunId,
    string JobId,
    string Repo,
    DateTimeOffset? LastHeartbeatAt,
    string Reason,
    string ContainerState,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxVanished, Timestamp);
