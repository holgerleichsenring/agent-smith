using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

/// <summary>The Docker sandbox backend's service registrations.</summary>
internal static class DockerSandboxRegistrations
{
    internal static void Register(IServiceCollection services)
    {
        var options = BuildOptions();
        services.AddSingleton(options);
        // p0465: ownership is the identity of the liveness store, derived from the
        // Redis endpoint the sandbox container itself is handed (--redis-url).
        services.AddSingleton(new SandboxOwnerIdentityResolver().Resolve(options.RedisUrl));
        services.AddSingleton<DockerSandboxQuery>();
        services.AddSingleton<DockerSandboxRemover>();
        services.AddSingleton<LiveRunSetReader>();
        services.AddSingleton<DockerSocketUriResolver>();
        services.AddSingleton<IDockerClient>(sp =>
        {
            var opts = sp.GetRequiredService<DockerSandboxOptions>();
            var uri = sp.GetRequiredService<DockerSocketUriResolver>().Resolve(opts.DockerSocketUri);
            return new DockerClientConfiguration(uri).CreateClient();
        });
        services.AddSingleton<DockerContainerSpecBuilder>();
        services.AddSingleton<DockerPackageCaches>();
        services.AddSingleton<DockerImagePresence>();
        RegisterFactory(services);
        RegisterLiveness(services);
    }

    private static void RegisterFactory(IServiceCollection services)
    {
        services.AddSingleton<ISandboxFactory>(sp => new DockerSandboxFactory(
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<DockerContainerSpecBuilder>(),
            sp.GetRequiredService<DockerSandboxOptions>(),
            sp.GetRequiredService<DockerPackageCaches>(),
            sp.GetRequiredService<DockerImagePresence>(),
            sp.GetRequiredService<IOptions<SandboxGlobalConfig>>(),
            sp.GetRequiredService<ILoggerFactory>()));
        // p0269a: Docker capacity is a configured concurrent-sandbox cap (no
        // create-time signal on a limitless daemon). Replaces the Unbounded default.
        services.AddSingleton<ISandboxCapacityProbe>(sp => new DockerCapacityProbe(
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<DockerSandboxQuery>(),
            sp.GetRequiredService<DockerSandboxOptions>(),
            sp.GetRequiredService<ILogger<DockerCapacityProbe>>()));
    }

    private static void RegisterLiveness(IServiceCollection services)
    {
        // p0201: Server composition swaps the no-op supervisor for the real
        // Docker variant. Per-pipeline-run lifetime (matches the coordinator).
        services.RemoveAll<ISandboxLivenessSupervisor>();
        services.AddTransient<ISandboxLivenessSupervisor>(sp => new SandboxLivenessSupervisor(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<IDockerClient>(),
            sp.GetRequiredService<IRunCancellationRegistry>(),
            sp.GetRequiredService<IEventPublisher>(),
            sp.GetRequiredService<ILoggerFactory>()));
        // p0465: the Docker backend is AUTO-DETECTED from /var/run/docker.sock, so this
        // used to arm a reaper on every dev machine and every side-instance. It now runs
        // only where the liveness store can actually answer 'is this run alive?'.
        var activation = SandboxReaperActivation.Decide(
            AnswersLiveness(services), Environment.GetEnvironmentVariable(SandboxReaperActivation.OverrideEnvVar));
        services.AddSingleton(activation);
        if (!activation.ShouldRun)
        {
            services.AddHostedService<SandboxReaperStandDownNotice>();
            return;
        }
        // p0391b: the reaper takes IDockerClient by constructor, so the host builds it
        // before it starts anything — which is why a malformed DOCKER_HOST used to kill
        // the whole server rather than the Docker backend.
        services.AddHostedService<SandboxOrphanReaper>();
    }

    // The relational lease is registered before AddSandbox (see ServerCompositionBuilder),
    // so the binding in force here is the one the reaper would get. NoOpActiveRunLease
    // reports no active runs at all — with it every sandbox of every live run looks dead.
    private static bool AnswersLiveness(IServiceCollection services) =>
        services.LastOrDefault(d => d.ServiceType == typeof(IActiveRunLease)) is { } lease
        && LeaseType(lease) != typeof(NoOpActiveRunLease);

    private static Type? LeaseType(ServiceDescriptor descriptor) =>
        descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();

    private static DockerSandboxOptions BuildOptions() => new()
    {
        RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379",
        DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? DockerSocketUriResolver.DefaultSocket,
        Network = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? "",
        MaxConcurrentSandboxes =
            int.TryParse(Environment.GetEnvironmentVariable("SANDBOX_MAX_CONCURRENT"), out var cap)
                ? cap
                : new DockerSandboxOptions().MaxConcurrentSandboxes,
        // p0407: warm package caches are what every run wants; the switch exists for
        // the operator who wants a provably cold restore or is short on disk.
        PackageCacheEnabled =
            !bool.TryParse(Environment.GetEnvironmentVariable("SANDBOX_PACKAGE_CACHE"), out var cache) || cache
    };
}
