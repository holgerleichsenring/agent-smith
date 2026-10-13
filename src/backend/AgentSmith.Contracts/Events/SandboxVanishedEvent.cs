namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0201: published by SandboxLivenessWatcher when the sandbox container is
/// confirmed gone (heartbeat missing for &gt;3 ticks AND docker-inspect either
/// returns 404 or reports a non-Running state). Lands on the run stream so the
/// trail carries the verdict; <see cref="ContainerState"/> distinguishes
/// "Exited(137)" (OOM kill) from "Gone" (docker daemon lost the container) so
/// the operator doesn't have to docker-log dive.
/// 2026-10-13-9a59i: ExitCode and OomKilled are the probe's exit facts; null when it had none.
/// </summary>
public sealed record SandboxVanishedEvent(
    string RunId,
    string JobId,
    string Repo,
    DateTimeOffset? LastHeartbeatAt,
    string Reason,
    string ContainerState,
    DateTimeOffset Timestamp,
    int? ExitCode = null,
    bool? OomKilled = null)
    : RunEvent(RunId, EventType.SandboxVanished, Timestamp);
