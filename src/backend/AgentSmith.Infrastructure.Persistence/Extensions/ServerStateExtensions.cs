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
        services.TryAddSingleton(TimeProvider.System);
        services.RemoveAll<IConnectionRepoSnapshot>().RemoveAll<IConnectionRepoSnapshotStore>();
        services.AddSingleton<DbConnectionRepoSnapshot>();
        services.AddSingleton<IConnectionRepoSnapshot>(sp => sp.GetRequiredService<DbConnectionRepoSnapshot>());
        return services.AddSingleton<IConnectionRepoSnapshotStore>(sp => sp.GetRequiredService<DbConnectionRepoSnapshot>());
    }
}
