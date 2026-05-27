using AgentSmith.Contracts.Sandbox;
using k8s;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

public sealed class KubernetesSandboxFactory(
    IKubernetes client,
    IConnectionMultiplexer redis,
    PodSpecBuilder podSpecBuilder,
    KubernetesSandboxOptions options,
    ILoggerFactory loggerFactory) : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var podName = $"agentsmith-sandbox-{jobId[..12]}";
        var pod = podSpecBuilder.Build(podName, jobId, options.RedisUrl, spec, options.OwnerReference);

        await client.CoreV1.CreateNamespacedPodAsync(pod, options.Namespace, cancellationToken: cancellationToken);
        loggerFactory.CreateLogger<KubernetesSandboxFactory>()
            .LogInformation("Sandbox pod {Pod} created in namespace {Ns}", podName, options.Namespace);

        var watcher = new KubernetesPodWatcher(client, loggerFactory.CreateLogger<KubernetesPodWatcher>());
        await watcher.WaitForReadyAsync(podName, options.Namespace,
            TimeSpan.FromSeconds(spec.TimeoutSeconds), cancellationToken);

        var channel = new SandboxRedisChannel(redis, jobId, loggerFactory.CreateLogger<SandboxRedisChannel>());
        return new KubernetesSandbox(client, options.Namespace, podName, jobId, channel,
            loggerFactory.CreateLogger<KubernetesSandbox>());
    }
}
