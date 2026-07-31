using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Server.Services.Preflight;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0324: the same checks `agentsmith doctor` runs, executed once at startup WARN-ONLY —
/// a failed check logs its fix hint and shows on /health, never blocks the host. Must be
/// registered AFTER AddJobSpawnerAsync: the sandbox probe delegates to the composed spawner.
/// </summary>
internal static class ServerPreflightExtensions
{
    internal static IServiceCollection AddServerPreflight(this IServiceCollection services)
    {
        services.AddPreflight();
        services.AddSingleton<IPreflightSandboxProbe, JobSpawnerSandboxProbe>();
        // p0355: server-only memory-floor check — a 256Mi pod OOMKills under normal load,
        // and each restart reaps the in-flight run + orphans its sandbox pods.
        services.AddSingleton<IPreflightCheck>(
            _ => new ServerMemoryFloorCheck(() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
        services.AddSingleton<IPreflightInfraProbe, InfraConnectivityProbe>();
        services.AddSingleton<PreflightReportStore>();
        services.AddHostedService<PreflightStartupService>();
        return services;
    }
}
