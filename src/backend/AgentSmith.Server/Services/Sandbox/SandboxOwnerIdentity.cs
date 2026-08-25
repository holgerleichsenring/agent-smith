namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: WHO a sandbox belongs to. The identity is that of the LIVENESS STORE the
/// reaper judges against — never of the process, the host or the pod. A per-process
/// id would make a server's own containers foreign to it after a restart, which is
/// the one case an orphan reaper exists for (the happy path already removes the
/// container on dispose), and it would repeal p0355, where two replicas sharing one
/// store are supposed to clean up each other's corpses.
///
/// <see cref="Value"/> is a legal Kubernetes label value BY CONSTRUCTION (see
/// <see cref="SandboxOwnerIdentityResolver"/>): at most 63 characters, alphanumeric
/// at both ends. Docker accepts a superset, so one value serves both backends and
/// they cannot silently diverge.
/// </summary>
public sealed record SandboxOwnerIdentity(string Value);
