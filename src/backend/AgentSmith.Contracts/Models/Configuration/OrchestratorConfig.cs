using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Optional per-project orchestrator container overrides. When unset, the
/// top-level <see cref="OrchestratorGlobalConfig"/> applies for image
/// registry/version, and <c>JobSpawner</c> options apply for resources.
/// Peer block to <see cref="SandboxConfig"/> — the orchestrator hosts the
/// pipeline-runner process, the sandbox hosts the per-language toolchain
/// in which agent skills execute.
/// </summary>
public sealed class OrchestratorConfig
{
    /// <summary>
    /// Per-project override of the orchestrator's container registry
    /// (e.g. <c>my-corp-mirror</c>). Null = inherit the top-level
    /// <c>orchestrator.registry</c> from agentsmith.yml.
    /// </summary>
    public string? Registry { get; set; }

    /// <summary>
    /// Per-project override of the orchestrator's image tag
    /// (e.g. <c>0.49.0-beta</c>). Null = inherit the top-level
    /// <c>orchestrator.version</c> from agentsmith.yml.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Per-project CPU + memory request/limit override for the orchestrator
    /// container. When null, the global <c>JobSpawner:Resources</c>
    /// configuration section applies; when that itself is unset,
    /// <see cref="ResourceLimits.Default"/> is used. Block is all-or-nothing:
    /// partial overrides are not supported — pick one resolution layer.
    /// </summary>
    public ResourceLimits? Resources { get; set; }
}
