namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: each PoC namespace's ResourceQuota, from <c>pocs.targets[].quota</c> — limits.cpu,
/// limits.memory, limits.ephemeral-storage and pods, Kubernetes quantities.</summary>
public sealed class PocQuotaConfig
{
    public string Cpu { get; set; } = "1";

    public string Memory { get; set; } = "1Gi";

    public int Pods { get; set; } = 4;

    public string EphemeralStorage { get; set; } = "2Gi";
}
