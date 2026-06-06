namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// Kubernetes-quantity resource specification for a sandbox container,
/// expressed as explicit request + limit pairs for cpu and memory.
/// Request controls scheduling and the QoS floor; Limit controls the
/// per-container cap and triggers OOM-kill / throttling when crossed.
/// Strings are passed through to Kubernetes verbatim (e.g. "250m", "512Mi");
/// Docker spawners parse them to bytes / nanoCpus at container-spec build time.
/// Settable properties (not a positional record) so YamlDotNet can deserialize
/// per-project blocks from agentsmith.yml under projects.&lt;name&gt;.sandbox.resources.
/// </summary>
public sealed record ResourceLimits
{
    /// <summary>Kubernetes-quantity CPU request (e.g. "250m"). Schedules the pod on a node with at least this much CPU.</summary>
    public string CpuRequest { get; set; } = "250m";

    /// <summary>Kubernetes-quantity CPU limit (e.g. "1000m"). Hard cap; container is CFS-throttled past this point.</summary>
    public string CpuLimit { get; set; } = "1000m";

    /// <summary>Kubernetes-quantity memory request (e.g. "512Mi"). Schedules the pod on a node with at least this much memory.</summary>
    public string MemoryRequest { get; set; } = "1Gi";

    /// <summary>
    /// Kubernetes-quantity memory limit (e.g. "4Gi"). Hard cap; the container is
    /// OOM-killed past this point. p0237: raised 2Gi → 4Gi — a real `dotnet build`
    /// / `npm build` (Roslyn + analyzers + MSBuild nodes, or a JS bundler) routinely
    /// peaks past 2Gi, and the kernel killed the sandbox mid-build, which surfaced
    /// as the run being cancelled ("sandbox-vanished"). Operators with big
    /// multi-project solutions raise it via projects.&lt;name&gt;.sandbox.resources.memory_limit.
    /// </summary>
    public string MemoryLimit { get; set; } = "4Gi";

    public ResourceLimits() { }

    public ResourceLimits(string cpuRequest, string cpuLimit, string memoryRequest, string memoryLimit)
    {
        CpuRequest = cpuRequest;
        CpuLimit = cpuLimit;
        MemoryRequest = memoryRequest;
        MemoryLimit = memoryLimit;
    }

    /// <summary>
    /// Safe defaults for a build-capable sandbox: Burstable QoS with a
    /// 250m / 1Gi floor and a 1000m / 4Gi cap. Used when neither
    /// projects.&lt;name&gt;.sandbox.resources nor the global SandboxOptions section
    /// in appsettings is set. (p0237 raised the memory cap from 2Gi after a real
    /// dotnet build OOM-killed the sandbox.)
    /// </summary>
    public static ResourceLimits Default { get; } = new();
}
