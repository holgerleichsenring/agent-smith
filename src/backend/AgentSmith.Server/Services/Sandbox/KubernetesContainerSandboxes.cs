using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the sandbox for one toolchain container of a running pod — its channel on its own job id, its
/// bus credential and step cap. Shared by the single sandbox factory (which owns its pod) and the workspace factory
/// (whose containers do not).
/// </summary>
public sealed class KubernetesContainerSandboxes(
    IKubernetes client,
    IConnectionMultiplexer redis,
    KubernetesSandboxOptions options,
    IOptions<SandboxGlobalConfig> sandboxConfig,
    IWireProtocolWatcher protocol,
    ILoggerFactory loggerFactory)
{
    public KubernetesSandbox Create(
        string podName, int index, PodToolchain toolchain, SandboxBusCredential bus, SandboxSpec spec, bool ownsPod)
    {
        var channel = new SandboxRedisChannel(
            redis, toolchain.JobId, loggerFactory.CreateLogger<SandboxRedisChannel>(), protocol);
        return new KubernetesSandbox(client, Address(podName, index), toolchain.JobId, channel, bus,
            // p0230: per-project resolved cap rides on the spec; fall back to global.
            spec.StepTimeoutSeconds ?? sandboxConfig.Value.StepTimeoutSeconds,
            toolchain.Image,
            // 2026-08-28-b630: the pod spec projected exactly these, so the sandbox
            // can be asked whether they arrived — by name, never by value.
            spec.Secrets ?? ResolvedSandboxSecrets.Empty,
            ownsPod,
            loggerFactory.CreateLogger<KubernetesSandbox>());
    }

    /// <summary>Deletes <paramref name="podName"/> — a workspace's last teardown step.</summary>
    public Task DeletePodAsync(string podName) =>
        new KubernetesPodRemoval(client, loggerFactory.CreateLogger<KubernetesPodRemoval>()).DeleteAsync(Address(podName, 0));

    private KubernetesContainerAddress Address(string podName, int index) =>
        new(options.Namespace, podName, PodToolchain.ContainerName(index));
}
