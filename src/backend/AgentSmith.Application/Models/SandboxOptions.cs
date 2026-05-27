using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Models;

/// <summary>
/// Global default CPU + memory request/limit values applied to every spawned
/// sandbox toolchain container when a project does not declare its own
/// projects.&lt;name&gt;.sandbox.resources block in agentsmith.yml.
/// Bound via IOptions&lt;SandboxOptions&gt; from configuration section "Sandbox"
/// (appsettings.json, environment variables with the Sandbox__ prefix, or any
/// other IConfiguration source).
/// </summary>
public sealed class SandboxOptions
{
    /// <summary>Kubernetes-quantity CPU request (e.g. "250m"). Schedules the pod on a node with at least this much CPU.</summary>
    public string CpuRequest { get; set; } = ResourceLimits.Default.CpuRequest;

    /// <summary>Kubernetes-quantity CPU limit (e.g. "1000m"). Hard cap; container is CFS-throttled past this point.</summary>
    public string CpuLimit { get; set; } = ResourceLimits.Default.CpuLimit;

    /// <summary>Kubernetes-quantity memory request (e.g. "512Mi"). Schedules the pod on a node with at least this much memory.</summary>
    public string MemoryRequest { get; set; } = ResourceLimits.Default.MemoryRequest;

    /// <summary>Kubernetes-quantity memory limit (e.g. "2Gi"). Hard cap; container is OOM-killed past this point.</summary>
    public string MemoryLimit { get; set; } = ResourceLimits.Default.MemoryLimit;

    /// <summary>Materialises the four properties into the shared <see cref="ResourceLimits"/> record consumed by spawners.</summary>
    public ResourceLimits ToResourceLimits() =>
        new(CpuRequest, CpuLimit, MemoryRequest, MemoryLimit);
}
