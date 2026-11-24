namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>2026-11-23-c81ed: the desired state on a PoC's row — what its deploy folder declares (Revision and digest
/// null before the first commit and after a delete commit; a null readiness path probes the port over TCP).</summary>
internal sealed record PocDesiredView(int Replicas, int? Revision, string? ImageDigest, string? ReadinessPath);
