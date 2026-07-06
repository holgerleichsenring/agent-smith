using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

internal static class SandboxBackendRegistrations
{
    internal static void RegisterKubernetes(IServiceCollection services)
    {
        services.AddSingleton<IKubernetes>(_ =>
        {
            var config = KubernetesClientConfiguration.InClusterConfig();
            return new Kubernetes(config);
        });
        services.AddSingleton<PodSpecBuilder>();
        services.AddSingleton(new KubernetesSandboxOptions
        {
            Namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "default",
            RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379"
        });
        services.AddSingleton<ISandboxFactory>(sp => new KubernetesSandboxFactory(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<PodSpecBuilder>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<IOptions<SandboxGlobalConfig>>(),
            sp.GetRequiredService<ILoggerFactory>()));
        // p0269a: admission reads the namespace ResourceQuota. Replaces the Unbounded
        // default registered in the Application composition.
        services.AddSingleton<ISandboxCapacityProbe>(sp => new KubernetesCapacityProbe(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<ILogger<KubernetesCapacityProbe>>()));
    }

    internal static void RegisterDocker(IServiceCollection services)
    {
        services.AddSingleton(new DockerSandboxOptions
        {
            RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379",
            DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock",
            Network = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? "",
            MaxConcurrentSandboxes =
                int.TryParse(Environment.GetEnvironmentVariable("SANDBOX_MAX_CONCURRENT"), out var cap)
                    ? cap
                    : new DockerSandboxOptions().MaxConcurrentSandboxes
        });
        services.AddSingleton<IDockerClient>(sp =>
        {
            var opts = sp.GetRequiredService<DockerSandboxOptions>();
            return new DockerClientConfiguration(new Uri(opts.DockerSocketUri)).CreateClient();
        });
        services.AddSingleton<DockerContainerSpecBuilder>();
        services.AddSingleton<ISandboxFactory>(sp => new DockerSandboxFactory(
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<DockerContainerSpecBuilder>(),
            sp.GetRequiredService<DockerSandboxOptions>(),
            sp.GetRequiredService<IOptions<SandboxGlobalConfig>>(),
            sp.GetRequiredService<ILoggerFactory>()));
        // p0269a: Docker capacity is a configured concurrent-sandbox cap (no
        // create-time signal on a limitless daemon). Replaces the Unbounded default.
        services.AddSingleton<ISandboxCapacityProbe>(sp => new DockerCapacityProbe(
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<DockerSandboxOptions>(),
            sp.GetRequiredService<ILogger<DockerCapacityProbe>>()));
        // p0201: Server composition swaps the no-op supervisor for the real
        // Docker variant. Per-pipeline-run lifetime (matches the coordinator).
        services.RemoveAll<ISandboxLivenessSupervisor>();
        services.AddTransient<ISandboxLivenessSupervisor>(sp => new SandboxLivenessSupervisor(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<IRunCancellationRegistry>(),
            sp.GetRequiredService<IEventPublisher>(),
            sp.GetRequiredService<ILoggerFactory>()));
        // p0201: orphan reaper as singleton hosted service.
        services.AddHostedService(sp => new SandboxOrphanReaper(
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<IActiveRunLease>(),
            sp.GetRequiredService<ILogger<SandboxOrphanReaper>>()));
    }
}
