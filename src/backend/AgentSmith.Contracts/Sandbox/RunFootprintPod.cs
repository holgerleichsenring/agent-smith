namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0336: one entry in a run's computed footprint. 2026-11-25-9992b: a toolchain container of the run's workspace pod
/// (the repositories it serves joined by '+', their contexts, the resolved image) or the browser pod.
/// CpuLimit/MemLimit are the RESOLVED k8s LIMITs (the OOM ceiling), the figure
/// admission reserves against — a run succeeds or OOM-dies against its limit.
/// </summary>
public sealed record RunFootprintPod(
    string Repo, IReadOnlyList<string> Contexts, string Image, string CpuLimit, string MemLimit);
