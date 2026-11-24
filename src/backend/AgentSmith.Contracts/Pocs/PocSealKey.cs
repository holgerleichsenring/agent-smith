using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: the refusal of pull_mode pull_secret without a master key — one sentence for the finding
/// and for the sealing step.</summary>
public static class PocSealKey
{
    public const string MasterKeyVariable = "AGENTSMITH_SECRET_MASTER_KEY";

    public static string Field(string target) => $"pocs.targets[{target}].registry.pull_mode";

    public static string NeedsMasterKey(string target) =>
        $"{PocRegistryConfig.Key}.pull_mode of target '{target}' is pull_secret, which needs a master key: the seal "
        + "fingerprint is an HMAC under a key derived from it, and without one every run would re-seal poc-pull and restart "
        + $"the pod; set {MasterKeyVariable} or use cluster_pulls";
}
