using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: swaps the fast-tier stub boundaries for the production
/// DockerSandboxFactory plus a per-test LocalGitSourceProvider. The real
/// IConnectionMultiplexer is already in the container via
/// ServerCompositionBuilder.AddRedis; we only swap the sandbox + source
/// provider. Reads REDIS_URL / DOCKER_HOST from the environment with
/// sensible local defaults.
/// </summary>
internal static class DockerHarnessRegistrations
{
    public static void Apply(IServiceCollection services, DockerHarnessSession session)
    {
        ReplaceSourceProvider(services, session);
        ReplaceSandboxFactory(services, session);
        ReplaceProjectAnalyzer(services);
        ReplaceProjectMapStore(services);
    }

    private static void ReplaceProjectAnalyzer(IServiceCollection services)
    {
        services.RemoveAll<IProjectAnalyzer>();
        services.AddSingleton<IProjectAnalyzer, StubProjectAnalyzer>();
    }

    private static void ReplaceProjectMapStore(IServiceCollection services)
    {
        services.RemoveAll<IProjectMapStore>();
        services.AddSingleton<IProjectMapStore, NoOpProjectMapStore>();
    }

    private static void ReplaceSourceProvider(IServiceCollection services, DockerHarnessSession session)
    {
        services.RemoveAll<ISourceProviderFactory>();
        services.AddSingleton<ISourceProviderFactory>(new LocalGitSourceProviderFactory(session));
    }

    private static void ReplaceSandboxFactory(IServiceCollection services, DockerHarnessSession session)
    {
        var options = BuildDockerOptions();
        services.RemoveAll<ISandboxFactory>();
        services.RemoveAll<IDockerClient>();
        services.RemoveAll<DockerSandboxOptions>();
        services.RemoveAll<DockerContainerSpecBuilder>();

        services.AddSingleton(options);
        services.AddSingleton<IDockerClient>(
            new DockerClientConfiguration(new Uri(options.DockerSocketUri)).CreateClient());
        services.AddSingleton<DockerContainerSpecBuilder>();

        services.AddSingleton<ISandboxFactory>(sp =>
        {
            var inner = new DockerSandboxFactory(
                sp.GetRequiredService<IDockerClient>(),
                sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
                sp.GetRequiredService<DockerContainerSpecBuilder>(),
                sp.GetRequiredService<DockerSandboxOptions>(),
                sp.GetRequiredService<IOptions<SandboxGlobalConfig>>(),
                sp.GetRequiredService<ILoggerFactory>());
            return new ExtraBindsSandboxFactory(inner, session);
        });
    }

    private static DockerSandboxOptions BuildDockerOptions() => new()
    {
        // RedisUrl is consumed by the sandbox container (passed as --redis-url
        // to the agent inside), so it must be reachable from INSIDE docker.
        // Default: the alias 'redis' resolves on the operator's deploy network
        // (docker-compose names it deploy_default; the redis container declares
        // alias 'redis'). Operators on a different layout override via env.
        // The host-side IConnectionMultiplexer uses REDIS_URL directly (in
        // RedisExtensions), where localhost is correct for the developer loop.
        RedisUrl = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_REDIS_URL") ?? "redis:6379",
        DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock",
        Network = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_NETWORK")
                  ?? Environment.GetEnvironmentVariable("DOCKER_NETWORK")
                  ?? "deploy_default",
    };
}
