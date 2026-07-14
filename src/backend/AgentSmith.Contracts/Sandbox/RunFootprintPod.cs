namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0336: one pod in a run's computed footprint — a toolchain-group sandbox (its
/// repo + the context(s) it serves + the resolved image) or the orchestrator.
/// CpuLimit/MemLimit are the RESOLVED k8s LIMITs (the OOM ceiling), the figure
/// admission reserves against — a run succeeds or OOM-dies against its limit.
/// </summary>
public sealed record RunFootprintPod(
    string Repo, IReadOnlyList<string> Contexts, string Image, string CpuLimit, string MemLimit);
