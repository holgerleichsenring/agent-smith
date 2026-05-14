using AgentSmith.Contracts.Sandbox;

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

    /// <summary>
    /// Legacy orchestrator image override. Default empty — the canonical
    /// resolution path runs through <c>IOrchestratorImageResolver</c> against
    /// <see cref="OrchestratorGlobalConfig"/> / <see cref="OrchestratorConfig"/>
    /// at intent-handling time. When this field is set (via the legacy
    /// <c>AGENTSMITH_IMAGE</c> env-var or the <c>JobSpawner:Image</c>
    /// configuration key) a deprecation warning is emitted at startup
    /// directing operators to the orchestrator config block; this field
    /// is otherwise unused.
    /// </summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>Image pull policy. Use IfNotPresent locally, Always in prod.</summary>
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>K8s Secret name containing API tokens. Only used by KubernetesJobSpawner.</summary>
    public string SecretName { get; set; } = "agentsmith-secrets";

    /// <summary>Seconds after job completion before K8s cleans it up. Only used by KubernetesJobSpawner.</summary>
    public int TtlSecondsAfterFinished { get; set; } = 300;

    /// <summary>Docker network to attach spawned containers to. Only used by DockerJobSpawner.</summary>
    public string DockerNetwork { get; set; } = string.Empty;

    /// <summary>
    /// CPU + memory request/limit for the spawned orchestrator container
    /// (the agentsmith-cli pod that runs the pipeline). Defaults to
    /// <see cref="ResourceLimits.Default"/>; configurable globally via the
    /// JobSpawner:Resources configuration section. Distinct from
    /// <c>Sandbox</c>/<c>SandboxOptions</c> which governs the per-language
    /// toolchain container — the orchestrator and the toolchain are two
    /// separate K8s workloads per ticket run.
    /// </summary>
    public ResourceLimits Resources { get; set; } = ResourceLimits.Default;
}
