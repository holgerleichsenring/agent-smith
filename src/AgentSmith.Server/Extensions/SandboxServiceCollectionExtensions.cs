using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

internal static class SandboxServiceCollectionExtensions
{
    /// <summary>
    /// Auto-detects the sandbox backend at startup. Order:
    ///   SANDBOX_TYPE explicit > KUBERNETES_SERVICE_HOST > inprocess fallback.
    /// Docker backend is deferred to a follow-up phase.
    /// </summary>
    internal static IServiceCollection AddSandbox(this IServiceCollection services)
    {
        var backend = ResolveBackend();
        switch (backend)
        {
            case SandboxBackend.Kubernetes:
                RegisterKubernetes(services);
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
        if (string.Equals(explicitType, "inprocess", StringComparison.OrdinalIgnoreCase))
            return SandboxBackend.InProcess;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            return SandboxBackend.Kubernetes;
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
}

internal enum SandboxBackend
{
    InProcess,
    Kubernetes
}

internal sealed record SandboxBackendInfo(SandboxBackend Backend);
