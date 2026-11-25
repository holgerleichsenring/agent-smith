using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Server.Services.Startup;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// The Kubernetes sandbox backend's service registrations. The client is in-cluster when the
/// server runs in a pod and reads the kubeconfig otherwise, so an out-of-cluster server still
/// creates its sandboxes.
/// </summary>
internal static class KubernetesSandboxRegistrations
{
    internal static void Register(IServiceCollection services)
    {
        var options = new KubernetesSandboxOptionsReader().Read();
        services.AddSingleton<IKubernetes>(_ => new Kubernetes(KubernetesClientConfigLoader.ForThisProcess().Load()));
        services.AddSingleton(options);
        // p0465: ownership is the identity of the liveness store, derived from the server's own
        // Redis endpoint (2026-10-25-c5a7c: not SANDBOX_REDIS_URL, so existing labels keep their value).
        services.AddSingleton(new SandboxOwnerIdentityResolver().Resolve(options.OwnerRedisUrl));
        services.AddSingleton<IStartupProbe, SandboxNamespaceProbe>();
        services.AddSingleton<SandboxPodLabels>();
        services.AddSingleton<LiveRunSetReader>();
        services.AddSingleton<PodSecurity>();
        services.AddSingleton<PodSpecBuilder>();
        services.AddSingleton(sp => new KubernetesPodLauncher(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(sp => new KubernetesContainerSandboxes(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<IOptions<SandboxGlobalConfig>>(),
            sp.GetRequiredService<IWireProtocolWatcher>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<ISandboxFactory, KubernetesSandboxFactory>();
        // 2026-11-25-9992b: a run's workspace — one pod, a container per image — on the same pod and container code.
        services.AddSingleton<IWorkspaceFactory, KubernetesWorkspaceFactory>();
        // p0269a: admission reads the namespace ResourceQuota. Replaces the Unbounded
        // default registered in the Application composition.
        services.AddSingleton<ISandboxCapacityProbe>(sp => new KubernetesCapacityProbe(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<ILogger<KubernetesCapacityProbe>>()));
        services.AddSingleton<IPreflightSandboxProbe, KubernetesSandboxBackendProbe>();
        // p0355: the corpse-pod sweep. Replaces the no-op default so lingering
        // sandbox pods (owning replica gone) stop holding the ResourceQuota.
        services.RemoveAll<ISandboxCorpseReaper>();
        services.AddSingleton<ISandboxCorpseReaper>(sp => new KubernetesSandboxCorpseReaper(
            sp.GetRequiredService<IKubernetes>(),
            sp.GetRequiredService<KubernetesSandboxOptions>(),
            sp.GetRequiredService<SandboxPodLabels>(),
            sp.GetRequiredService<LiveRunSetReader>(),
            sp.GetRequiredService<HeldConversationReader>(),
            sp.GetRequiredService<SandboxBusCredentials>(),
            sp.GetRequiredService<ILogger<KubernetesSandboxCorpseReaper>>()));
    }
}
