using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Models.Configuration;
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
    internal static IServiceCollection AddServerPreflight(
        this IServiceCollection services, TokenAuthorityConfig auth)
    {
        services.AddPreflight();
        services.AddSingleton<IPreflightSandboxProbe, JobSpawnerSandboxProbe>();
        // p0355: server-only memory-floor check — a 256Mi pod OOMKills under normal load,
        // and each restart reaps the in-flight run + orphans its sandbox pods.
        services.AddSingleton<IPreflightCheck>(
            _ => new ServerMemoryFloorCheck(() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
        // 2026-09-14-3f5b: server-only for the same reason the memory floor is — two of its
        // four facts do not exist in the CLI's graph. It is handed the COMPOSED auth block
        // rather than resolving one: p0503e measured that the registered TokenAuthorityConfig
        // is read lazily from the environment, so a component built later can measure a
        // different authority than the handler validates against, and enforcement is the
        // axis this check turns on.
        services.AddSingleton<IPreflightCheck>(sp => ActivatorUtilities.CreateInstance<SignInCheck>(sp, auth));
        services.AddSingleton<IPreflightInfraProbe, InfraConnectivityProbe>();
        services.AddSingleton<PreflightReportStore>();
        services.AddHostedService<PreflightStartupService>();
        return services;
    }
}
