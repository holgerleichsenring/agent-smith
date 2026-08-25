using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Sandbox;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

/// <summary>The Kubernetes sandbox backend's service registrations.</summary>
internal static class KubernetesSandboxRegistrations
{
    internal static void Register(IServiceCollection services)
    {
        var options = new KubernetesSandboxOptions
        {
            Namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "default",
            RedisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379"
        };
        services.AddSingleton<IKubernetes>(_ => new Kubernetes(KubernetesClientConfiguration.InClusterConfig()));
        services.AddSingleton(options);
        // p0465: ownership is the identity of the liveness store, derived from the
        // Redis endpoint the sandbox itself is handed.
        services.AddSingleton(new SandboxOwnerIdentityResolver().Resolve(options.RedisUrl));
        services.AddSingleton<SandboxPodLabels>();
        services.AddSingleton<LiveRunSetReader>();
        services.AddSingleton<PodSpecBuilder>();
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
        // p0355: the corpse-pod sweep. Replaces the no-op default so lingering
        // sandbox pods (owning replica gone) stop holding the ResourceQuota.
        services.RemoveAll<ISandboxCorpseReaper>();
        services.AddSingleton<ISandboxCorpseReaper>(sp => new KubernetesSandboxCorpseReaper(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<SandboxPodLabels>(),
            sp.GetRequiredService<LiveRunSetReader>(),
            sp.GetRequiredService<ILogger<KubernetesSandboxCorpseReaper>>()));
    }
}
