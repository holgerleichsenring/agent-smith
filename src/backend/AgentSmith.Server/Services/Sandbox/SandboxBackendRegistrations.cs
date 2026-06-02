using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using k8s;
using Microsoft.Extensions.DependencyInjection;
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
    }

    internal static void RegisterDocker(IServiceCollection services)
    {
        services.AddSingleton(new DockerSandboxOptions
        {
            RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379",
            DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock",
            Network = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? ""
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
    }
}
