using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Long-running hosted services: queue consumer, housekeeping leader, poller
/// leader, and the Redis connection health monitor. Each one is published as
/// ISubsystemHealth so /health endpoints expose the per-subsystem status.
/// </summary>
internal static class PollingExtensions
{
    internal static IServiceCollection AddLongRunningServices(this IServiceCollection services)
    {
        services.AddSingleton<QueueConsumerHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<QueueConsumerHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<QueueConsumerHostedService>().Health);

        services.AddSingleton<HousekeepingLeaderHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HousekeepingLeaderHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<HousekeepingLeaderHostedService>().Health);

        services.AddSingleton<PollerLeaderHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<PollerLeaderHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<PollerLeaderHostedService>().Health);

        // p0281a: keep the connection repo snapshot warm (warm-on-start + interval).
        services.AddHostedService<RepoDiscoveryRefreshHostedService>();

        services.AddSingleton<RedisConnectionHealth>();
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<RedisConnectionHealth>().Health);

        return services;
    }
}
