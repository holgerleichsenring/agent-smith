namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: the Kubernetes secret types a PoC's secrets are written as.</summary>
public static class PocSecretTypes
{
    public const string Opaque = "Opaque";
    public const string DockerConfigJson = "kubernetes.io/dockerconfigjson";
}
