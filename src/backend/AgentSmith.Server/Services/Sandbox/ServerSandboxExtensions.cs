using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Sandbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Server sandbox composition: auto-detected backend factory (SANDBOX_TYPE &gt;
/// KUBERNETES_SERVICE_HOST &gt; /var/run/docker.sock &gt; InProcess fallback),
/// the sandbox global config + options bindings.
/// Per-backend service-registration helpers live in <see cref="DockerSandboxRegistrations"/>
/// and <see cref="KubernetesSandboxRegistrations"/>.
/// </summary>
internal static class ServerSandboxExtensions
{
    internal static IServiceCollection AddSandboxOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SandboxOptions>(configuration.GetSection("Sandbox"));
        // p0336: the app-level capacity budget the reservation ledger gates on.
        // Unset = unbounded (fail-open); set at or below the k8s ResourceQuota.
        services.Configure<CapacityBudgetOptions>(configuration.GetSection("CapacityBudget"));
        return services;
    }

    internal static IServiceCollection AddSandboxGlobalConfig(this IServiceCollection services)
    {
        services.AddSingleton<IOptions<SandboxGlobalConfig>, LoadedSandboxGlobalConfig>(); // read on first use, 283df
        return services;
    }

    internal static IServiceCollection AddSandbox(this IServiceCollection services)
    {
        var backend = ResolveBackend();
        // 2026-08-25-0d01: the reader for the protocol version every wire record has always
        // carried. Registered for every backend, because the in-process one is the only one
        // that cannot skew and the other two both talk to an image with its own release.
        services.AddSingleton<IWireProtocolWatcher, WireProtocolWatcher>();
        // 2026-09-22-2d11a: the reapers' held-conversation rail. Registered for every
        // backend because the rail is the judgement's, not a backend's; the relational
        // reader replaces the Application composition's empty default here, where the
        // reapers that use it are composed.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SandboxHoldWindowResolver>();
        services.AddSingleton<HeldConversationReader>();
        services.RemoveAll<IConversationLivenessReader>();
        services.AddSingleton<IConversationLivenessReader, DbConversationLivenessReader>();
        // 2026-09-22-2d11b: and the heartbeat a turn verifies a held sandbox through. Redis is
        // the agent's own liveness signal whichever backend runs the container, so it is
        // registered here beside the rail rather than per backend.
        services.RemoveAll<ISandboxHeartbeatProbe>();
        services.AddSingleton<ISandboxHeartbeatProbe, RedisSandboxHeartbeatProbe>();
        switch (backend)
        {
            case SandboxBackend.Kubernetes: KubernetesSandboxRegistrations.Register(services); break;
            case SandboxBackend.Docker: DockerSandboxRegistrations.Register(services); break;
            case SandboxBackend.InProcess: AddInProcess(services); break;
        }
        services.AddSingleton(new SandboxBackendInfo(backend));
        // 2026-10-01-283de: the in-process backend runs no image, so it has no browser runtime.
        services.AddSingleton(new SandboxContainerRuntime(backend != SandboxBackend.InProcess));
        return services;
    }

    private static void AddInProcess(IServiceCollection services)
    {
        services.AddSingleton<ISandboxFactory, InProcessSandboxFactory>();
        services.AddSingleton<IPreflightSandboxProbe, SandboxRoundTripProbe>();
    }

    private static SandboxBackend ResolveBackend()
    {
        var explicitType = Environment.GetEnvironmentVariable("SANDBOX_TYPE");
        if (string.Equals(explicitType, "kubernetes", StringComparison.OrdinalIgnoreCase)) return SandboxBackend.Kubernetes;
        if (string.Equals(explicitType, "docker", StringComparison.OrdinalIgnoreCase)) return SandboxBackend.Docker;
        if (string.Equals(explicitType, "inprocess", StringComparison.OrdinalIgnoreCase)) return SandboxBackend.InProcess;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"))) return SandboxBackend.Kubernetes;
        if (File.Exists("/var/run/docker.sock")) return SandboxBackend.Docker;
        return SandboxBackend.InProcess;
    }
}
