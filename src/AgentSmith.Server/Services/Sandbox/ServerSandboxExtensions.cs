using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Orchestrator;
using AgentSmith.Server.Services.Sandbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Server sandbox composition: auto-detected backend factory (SANDBOX_TYPE &gt;
/// KUBERNETES_SERVICE_HOST &gt; /var/run/docker.sock &gt; InProcess fallback),
/// sandbox/orchestrator global config + options bindings, IOrchestratorResourceResolver.
/// Per-backend service-registration helpers live in <see cref="SandboxBackendRegistrations"/>.
/// </summary>
internal static class ServerSandboxExtensions
{
    internal static IServiceCollection AddSandboxOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SandboxOptions>(configuration.GetSection("Sandbox"));
        return services;
    }

    internal static IServiceCollection AddSandboxGlobalConfig(this IServiceCollection services)
    {
        services.AddSingleton<IOptions<SandboxGlobalConfig>>(sp =>
        {
            var loader = sp.GetRequiredService<IConfigurationLoader>();
            var context = sp.GetRequiredService<ServerContext>();
            return Options.Create(loader.LoadConfig(context.ConfigPath).Sandbox);
        });
        return services;
    }

    internal static IServiceCollection AddOrchestratorGlobalConfig(this IServiceCollection services)
    {
        services.AddSingleton<IOptions<OrchestratorGlobalConfig>>(sp =>
        {
            var loader = sp.GetRequiredService<IConfigurationLoader>();
            var context = sp.GetRequiredService<ServerContext>();
            return Options.Create(loader.LoadConfig(context.ConfigPath).Orchestrator);
        });
        services.AddSingleton<IOrchestratorResourceResolver, OrchestratorResourceResolver>();
        return services;
    }

    internal static IServiceCollection AddSandbox(this IServiceCollection services)
    {
        var backend = ResolveBackend();
        switch (backend)
        {
            case SandboxBackend.Kubernetes: SandboxBackendRegistrations.RegisterKubernetes(services); break;
            case SandboxBackend.Docker: SandboxBackendRegistrations.RegisterDocker(services); break;
            case SandboxBackend.InProcess: services.AddSingleton<ISandboxFactory, InProcessSandboxFactory>(); break;
        }
        services.AddSingleton(new SandboxBackendInfo(backend));
        return services;
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
