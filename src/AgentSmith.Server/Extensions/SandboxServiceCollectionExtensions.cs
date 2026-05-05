using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

internal static class SandboxServiceCollectionExtensions
{
    /// <summary>
    /// Auto-detects the sandbox backend at startup. Order:
    ///   SANDBOX_TYPE explicit > KUBERNETES_SERVICE_HOST > /var/run/docker.sock > inprocess fallback.
    /// </summary>
    internal static IServiceCollection AddSandbox(this IServiceCollection services)
    {
        var backend = ResolveBackend();
        switch (backend)
        {
            case SandboxBackend.Kubernetes:
                RegisterKubernetes(services);
                break;
            case SandboxBackend.Docker:
                RegisterDocker(services);
                break;
            case SandboxBackend.InProcess:
                services.AddSingleton<ISandboxFactory, InProcessSandboxFactory>();
                break;
        }
        services.AddSingleton(new SandboxBackendInfo(backend));
        return services;
    }

    private static SandboxBackend ResolveBackend()
    {
        var explicitType = Environment.GetEnvironmentVariable("SANDBOX_TYPE");
        if (string.Equals(explicitType, "kubernetes", StringComparison.OrdinalIgnoreCase))
            return SandboxBackend.Kubernetes;
        if (string.Equals(explicitType, "docker", StringComparison.OrdinalIgnoreCase))
            return SandboxBackend.Docker;
        if (string.Equals(explicitType, "inprocess", StringComparison.OrdinalIgnoreCase))
            return SandboxBackend.InProcess;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            return SandboxBackend.Kubernetes;
        if (File.Exists("/var/run/docker.sock"))
            return SandboxBackend.Docker;
        return SandboxBackend.InProcess;
    }

    private static void RegisterKubernetes(IServiceCollection services)
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
            sp.GetRequiredService<ILoggerFactory>()));
    }

    private static void RegisterDocker(IServiceCollection services)
    {
        services.AddSingleton(new DockerSandboxOptions
        {
            RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379",
            DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock"
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
            sp.GetRequiredService<ILoggerFactory>()));
    }
}

internal enum SandboxBackend
{
    InProcess,
    Kubernetes,
    Docker
}

internal sealed record SandboxBackendInfo(SandboxBackend Backend);
