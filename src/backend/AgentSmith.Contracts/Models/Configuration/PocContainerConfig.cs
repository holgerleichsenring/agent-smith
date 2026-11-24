namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: the resources of each PoC's container, from <c>pocs.targets[].container</c> — Kubernetes
/// quantities, rendered into the Deployment's requests and limits.</summary>
public sealed class PocContainerConfig
{
    public string CpuRequest { get; set; } = "50m";

    public string CpuLimit { get; set; } = "500m";

    public string MemoryRequest { get; set; } = "128Mi";

    public string MemoryLimit { get; set; } = "512Mi";

    public string EphemeralLimit { get; set; } = "256Mi";
}
