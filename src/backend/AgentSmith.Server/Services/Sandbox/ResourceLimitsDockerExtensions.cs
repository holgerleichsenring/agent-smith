using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Converts <see cref="ResourceLimits"/> Kubernetes-quantity strings into the
/// integer types Docker's HostConfig expects. Both <c>DockerContainerSpecBuilder</c>
/// (sandbox toolchain) and <c>DockerJobSpawner</c> (orchestrator) parse the same
/// quantities. p0268: the parse itself now lives in <see cref="KubernetesQuantity"/>
/// (Contracts) so the Application-layer resolver validates with the same logic; this
/// extension keeps the historic "0 on malformed input" behavior by mapping false → 0.
/// </summary>
internal static class ResourceLimitsDockerExtensions
{
    public static long CpuLimitToNanoCpus(this ResourceLimits limits)
        => KubernetesQuantity.TryParseCpuToNanoCpus(limits.CpuLimit, out var nanoCpus) ? nanoCpus : 0;

    public static long MemoryLimitToBytes(this ResourceLimits limits)
        => KubernetesQuantity.TryParseMemoryToBytes(limits.MemoryLimit, out var bytes) ? bytes : 0;

    public static long MemoryRequestToBytes(this ResourceLimits limits)
        => KubernetesQuantity.TryParseMemoryToBytes(limits.MemoryRequest, out var bytes) ? bytes : 0;
}
