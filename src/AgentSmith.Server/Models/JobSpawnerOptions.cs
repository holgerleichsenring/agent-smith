namespace AgentSmith.Server.Models;

/// <summary>
/// Configuration options for job spawning, shared by both KubernetesJobSpawner
/// and DockerJobSpawner where applicable. Bound via IOptions&lt;JobSpawnerOptions&gt;
/// from configuration section "JobSpawner" (appsettings.json, environment
/// variables with the JobSpawner__ prefix, or any other IConfiguration source).
/// </summary>
public sealed class JobSpawnerOptions
{
    /// <summary>Kubernetes namespace for spawned jobs. Only used by KubernetesJobSpawner.</summary>
    public string Namespace { get; set; } = "default";

    /// <summary>Agent container image name.</summary>
    public string Image { get; set; } = "agentsmith-cli:latest";

    /// <summary>Image pull policy. Use IfNotPresent locally, Always in prod.</summary>
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>K8s Secret name containing API tokens. Only used by KubernetesJobSpawner.</summary>
    public string SecretName { get; set; } = "agentsmith-secrets";

    /// <summary>Seconds after job completion before K8s cleans it up. Only used by KubernetesJobSpawner.</summary>
    public int TtlSecondsAfterFinished { get; set; } = 300;

    /// <summary>Docker network to attach spawned containers to. Only used by DockerJobSpawner.</summary>
    public string DockerNetwork { get; set; } = string.Empty;
}
