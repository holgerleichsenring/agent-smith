namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: the egress proxy a PoC reaches the outside through, from
/// <c>pocs.targets[].network.egress_proxy</c> — the URL its containers get as HTTP(S)_PROXY, the pod port the
/// NetworkPolicy opens, and where the proxy runs: a namespace plus pod labels, or a CIDR (exactly one form).</summary>
public sealed class PocEgressProxyConfig
{
    public const string DefaultNoProxy = "localhost,127.0.0.1,.svc,.cluster.local";

    public string? Url { get; set; }

    /// <summary>The proxy POD's port — a NetworkPolicy sees the port after the Service translated it.</summary>
    public int Port { get; set; } = 3128;

    public string? Namespace { get; set; }

    public Dictionary<string, string> PodLabels { get; set; } = [];

    public string? Cidr { get; set; }

    public string NoProxy { get; set; } = DefaultNoProxy;

    /// <summary>Whether the proxy is named by namespace and pod labels (else by CIDR).</summary>
    public bool BySelector => !string.IsNullOrWhiteSpace(Namespace) || PodLabels.Count > 0;
}
