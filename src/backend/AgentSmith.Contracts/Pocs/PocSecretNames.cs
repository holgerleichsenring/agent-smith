namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: the names of a PoC's objects in its namespace — the sealed secret variables, the sealed
/// pull secret and the plain variables' ConfigMap. The c81e series renders from these only.</summary>
public static class PocSecretNames
{
    public const string Secrets = "poc-secrets";
    public const string Pull = "poc-pull";
    public const string Config = "poc-config";

    /// <summary>The entry key of a kubernetes.io/dockerconfigjson secret.</summary>
    public const string DockerConfigJsonKey = ".dockerconfigjson";
}
