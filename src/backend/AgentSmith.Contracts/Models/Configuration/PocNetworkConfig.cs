namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: what each PoC's NetworkPolicy admits, from <c>pocs.targets[].network</c> — ingress only
/// from the ingress controller's namespace; egress only to the cluster DNS and the egress proxy.</summary>
public sealed class PocNetworkConfig
{
    /// <summary>The namespace the ingress controller runs in (its kubernetes.io/metadata.name label). Required while on.</summary>
    public string? IngressControllerNamespace { get; set; }

    public string DnsNamespace { get; set; } = "kube-system";

    public Dictionary<string, string> DnsPodLabels { get; set; } = new() { ["k8s-app"] = "kube-dns" };

    public PocEgressProxyConfig EgressProxy { get; set; } = new();
}
