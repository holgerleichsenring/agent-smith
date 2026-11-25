using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace on Kubernetes — ONE pod with a toolchain container per image, each its own
/// agent, job id and per-job bus credential, all mounting the pod's one /work. The pod is named after the first
/// container's job and deleted once, by the workspace, after every container's agent ended.
/// </summary>
public sealed class KubernetesWorkspaceFactory(
    PodSpecBuilder podSpecBuilder,
    KubernetesPodLauncher launcher,
    KubernetesContainerSandboxes containers,
    KubernetesSandboxOptions options,
    SandboxBusCredentials busCredentials,
    WorkspaceAgentProtocolGate protocolGate,
    ILoggerFactory loggerFactory) : IWorkspaceFactory
{
    public async Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        if (options.Misconfiguration is { } misconfiguration)
            throw new InvalidOperationException(misconfiguration); // 2026-10-25-c5a7c
        if (spec.Toolchains.Count == 0) throw new ArgumentException("A workspace needs a toolchain.", nameof(spec));
        var issued = new List<(PodToolchain Toolchain, SandboxBusCredential Bus)>();
        foreach (var toolchain in spec.Toolchains) // 4778: every credential before the pod
        {
            var jobId = Guid.NewGuid().ToString("N");
            var bus = await busCredentials.IssueAsync(jobId, options.RedisUrl, spec.Base.RequiresPerJobBus);
            issued.Add((new PodToolchain(toolchain.Image, toolchain.Resources, jobId, bus.Url), bus));
        }
        var podName = $"agentsmith-sandbox-{issued[0].Toolchain.JobId[..12]}";
        var pod = podSpecBuilder.Build(podName, [.. issued.Select(i => i.Toolchain)], spec.Base, options.OwnerReference);
        await launcher.LaunchAsync(
            pod, TimeSpan.FromSeconds(spec.Base.TimeoutSeconds), spec.Base.ImagePullSecrets, cancellationToken);
        var sandboxes = issued.Select((entry, index) => containers.Create(podName, index, entry.Toolchain,
            entry.Bus, spec.Base with { ToolchainImage = entry.Toolchain.Image, Resources = entry.Toolchain.Resources },
            ownsPod: false)).ToList();
        await RefuseOldAgentsAsync(spec, podName, sandboxes, cancellationToken);
        return new ContainerWorkspace(spec.Toolchains, [.. sandboxes], () => containers.DeletePodAsync(podName),
            loggerFactory.CreateLogger<ContainerWorkspace>());
    }

    // 2026-11-25-9992d: a project that scopes secrets refuses an agent below protocol 3; the pod and every bus
    // credential go with the refusal, as they would with the workspace.
    private async Task RefuseOldAgentsAsync(
        WorkspaceSpec spec, string podName, IReadOnlyList<KubernetesSandbox> sandboxes, CancellationToken ct)
    {
        try
        {
            await protocolGate.RefuseOldAgentsAsync(
                spec.Base, [.. sandboxes.Select(s => (s.ToolchainImage, (ISandbox)s))], ct);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger<KubernetesWorkspaceFactory>()
                .LogWarning(ex, "Workspace pod {Pod} refused; removing it", podName);
            foreach (var sandbox in sandboxes)
                await RemoveAsync(() => sandbox.ForceRemoveAsync(CancellationToken.None), podName);
            await RemoveAsync(() => containers.DeletePodAsync(podName), podName);
            throw;
        }
    }

    // A failed removal is logged and the next one still runs, so the refusal is what the caller sees.
    private async Task RemoveAsync(Func<Task> removal, string podName)
    {
        try { await removal(); }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger<KubernetesWorkspaceFactory>()
                .LogWarning(ex, "Removing part of refused workspace pod {Pod} failed", podName);
        }
    }
}
