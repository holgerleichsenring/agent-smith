using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// A single sandbox on Kubernetes: a pod with one toolchain container, deleted with the sandbox. 2026-11-25-9992b: the
/// pod is created and awaited by <see cref="KubernetesPodLauncher"/> and its container sandbox built by
/// <see cref="KubernetesContainerSandboxes"/>, both shared with <see cref="KubernetesWorkspaceFactory"/>.
/// </summary>
public sealed class KubernetesSandboxFactory(
    PodSpecBuilder podSpecBuilder,
    KubernetesPodLauncher launcher,
    KubernetesContainerSandboxes containers,
    KubernetesSandboxOptions options,
    SandboxBusCredentials busCredentials) : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        if (options.Misconfiguration is { } misconfiguration)
            throw new InvalidOperationException(misconfiguration); // 2026-10-25-c5a7c
        var jobId = Guid.NewGuid().ToString("N");
        var podName = $"agentsmith-sandbox-{jobId[..12]}";
        // 2026-10-25-c5a7e: the sandbox reaches Redis as its own job's ACL user.
        var bus = await busCredentials.IssueAsync(jobId, options.RedisUrl, spec.RequiresPerJobBus); // 4778: before the pod
        var toolchain = new PodToolchain(spec.ToolchainImage, spec.Resources, jobId, bus.Url);
        var pod = podSpecBuilder.Build(podName, [toolchain], spec, options.OwnerReference);
        await launcher.LaunchAsync(pod, TimeSpan.FromSeconds(spec.TimeoutSeconds), spec.ImagePullSecrets, cancellationToken);
        return containers.Create(podName, 0, toolchain, bus, spec, ownsPod: true);
    }
}
