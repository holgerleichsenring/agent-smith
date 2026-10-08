using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// 2026-10-02-5ab2a: the server's state that must survive a Redis flush or restart, held in the
/// database. Server-only and chained after the relational persistence, so it replaces the core's
/// in-memory and disk defaults the CLI keeps.
/// </summary>
public static class ServerStateExtensions
{
    public static IServiceCollection AddServerState(this IServiceCollection services)
    {
        services.AddScoped<ConnectionDiscoveryRepository>();
        services.AddScoped<PendingClarificationRepository>(); // 2026-10-02-5ab2d
        services.AddScoped<WebhookLastSeenRepository>(); // 2026-10-02-5ab2e
        // 2026-10-08-0781: the rework nudge queue and its ledger.
        services.AddScoped<ReworkNudgeRepository>().AddScoped<ReworkLedgerRepository>();
        services.AddScoped<AgentSmith.Contracts.Sweep.ISweepCursors, SweepCursorStore>(); // 2026-10-08-9e6e
        services.RemoveAll<IReworkNudges>().RemoveAll<IReworkWatermark>();
        services.AddSingleton<DbReworkNudges>();
        services.AddSingleton<IReworkNudges>(sp => sp.GetRequiredService<DbReworkNudges>());
        services.AddSingleton<IReworkWatermark>(sp => sp.GetRequiredService<DbReworkNudges>());
        services.TryAddSingleton(TimeProvider.System);
        services.RemoveAll<IConnectionRepoSnapshot>().RemoveAll<IConnectionRepoSnapshotStore>();
        services.AddSingleton<DbConnectionRepoSnapshot>();
        services.AddSingleton<IConnectionRepoSnapshot>(sp => sp.GetRequiredService<DbConnectionRepoSnapshot>());
        return services.AddSingleton<IConnectionRepoSnapshotStore>(sp => sp.GetRequiredService<DbConnectionRepoSnapshot>());
    }
}
