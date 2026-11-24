namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: how a PoC target's Ingresses are served, from <c>pocs.targets[].ingress</c> — the ingress
/// class the controller answers to (required while PoCs are on) and, optionally, the cert-manager cluster issuer that
/// signs each PoC's TLS certificate.</summary>
public sealed class PocIngressConfig
{
    public string? Class { get; set; }

    /// <summary>A cert-manager ClusterIssuer; set, each Ingress names TLS secret 'app-tls' and the issuer annotation.</summary>
    public string? TlsIssuer { get; set; }
}
