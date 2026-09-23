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
        this IServiceCollection services, TokenAuthorityConfig? auth)
    {
        services.AddPreflight();
        services.AddSingleton<IPreflightSandboxProbe, JobSpawnerSandboxProbe>();
        // p0355: server-only memory-floor check — a 256Mi pod OOMKills under normal load,
        // and each restart reaps the in-flight run + orphans its sandbox pods.
        services.AddSingleton<IPreflightCheck>(
            _ => new ServerMemoryFloorCheck(() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
        services.AddSignInCheck(auth);
        services.AddSingleton<IPreflightInfraProbe, InfraConnectivityProbe>();
        services.AddSingleton<PreflightReportStore>();
        services.AddHostedService<PreflightStartupService>();
        return services;
    }

    /// <summary>
    /// 2026-09-14-3f5b: server-only for the same reason the memory floor is — two of its four
    /// facts do not exist in the CLI's graph. It is handed the COMPOSED auth block rather than
    /// resolving one: p0503e measured that the registered TokenAuthorityConfig is read lazily
    /// from the environment, so a component built later can measure a different authority than
    /// the handler validates against, and enforcement is the axis this check turns on.
    /// <para>
    /// 2026-09-23-2c60: which is why the coalesce lives here. An installation declaring no
    /// <c>auth:</c> key has no block to hand over, and that is the very state the check exists
    /// to report. The container's own registration of this type coalesces the same way; the
    /// composed block is a different one, so the coalesce travels with it.
    /// </para>
    /// <para>
    /// 2026-09-23-e7f0: the block reaches the check through a KEYED registration rather than a
    /// constructor argument. An argument was matched to a parameter by its RUNTIME type, and a
    /// null block has none, so the constructor was rejected and the host died at startup. A key
    /// names the parameter instead, and an INSTANCE — not a factory — keeps it the very object
    /// this composition read, which is the whole reason it is not simply resolved.
    /// </para>
    /// </summary>
    internal static IServiceCollection AddSignInCheck(
        this IServiceCollection services, TokenAuthorityConfig? auth)
    {
        services.AddKeyedSingleton<TokenAuthorityConfig>(
            SignInCheck.ComposedAuthorityKey, auth ?? new TokenAuthorityConfig());
        services.AddSingleton<IPreflightCheck, SignInCheck>();
        return services;
    }
}
