using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Long-running hosted services: queue consumer, housekeeping leader, poller
/// leader, and the Redis connection health monitor. Each one is published as
/// ISubsystemHealth so /health lists the per-subsystem status.
/// </summary>
internal static class PollingExtensions
{
    internal static IServiceCollection AddLongRunningServices(this IServiceCollection services)
    {
        services.AddSingleton<QueueConsumerHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<QueueConsumerHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<QueueConsumerHostedService>().Health);

        // 2026-10-02-5ab2c: one of the housekeeping leader's loops.
        services.AddSingleton<AgentSmith.Server.Services.Events.ActiveRunSetReseeder>();
        services.AddSingleton<HousekeepingLeaderHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HousekeepingLeaderHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<HousekeepingLeaderHostedService>().Health);

        services.AddSingleton<LeaderEpochLoop>(); // 2026-10-08-9e6e
        services.AddSingleton<PollerLeaderHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<PollerLeaderHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<PollerLeaderHostedService>().Health);

        // 2026-10-08-9e6e: polling mode's change sweep, under its own leader lease.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AgentSmith.Server.Services.Sweep.ChangeSweepHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentSmith.Server.Services.Sweep.ChangeSweepHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<AgentSmith.Server.Services.Sweep.ChangeSweepHostedService>().Health);

        services.AddSingleton<AgentSmith.Server.Services.Sweep.PrRecordReReadHostedService>(); // 2026-10-14-dbceg
        services.AddSingleton<AgentSmith.Server.Services.Deployment.DeployFollow>().AddSingleton<AgentSmith.Server.Services.Deployment.DeployFollowPass>() // 2026-11-18-3863f
            .AddScoped<AgentSmith.Server.Services.Deployment.DeployPromotion>(); // 2026-11-18-3863h
        services.TryAddScoped<AgentSmith.Server.Services.Deployment.DeployLauncher>(); // what a promotion launches through
        services.AddHostedService(sp => sp.GetRequiredService<AgentSmith.Server.Services.Sweep.PrRecordReReadHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp => sp.GetRequiredService<AgentSmith.Server.Services.Sweep.PrRecordReReadHostedService>().Health);

        // 2026-11-16-f3fbc: tickets stuck on a precondition start by themselves once it holds — leader-elected,
        // re-run on every config epoch by LeaderEpochLoop and every preconditions.recheck_interval_seconds.
        services.AddSingleton<AgentSmith.Server.Services.Preconditions.PreconditionHoldCheck>();
        services.AddSingleton<AgentSmith.Server.Services.Preconditions.PreconditionReleasePass>();
        services.AddSingleton<AgentSmith.Server.Services.Preconditions.PreconditionReleaseHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentSmith.Server.Services.Preconditions.PreconditionReleaseHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp => sp.GetRequiredService<AgentSmith.Server.Services.Preconditions.PreconditionReleaseHostedService>().Health);

        // 2026-11-17-713ec: a branch-cleanup run per project every branch_cleanup.interval_seconds — leader-elected,
        // re-run on every config epoch; the launcher is scoped (it writes the queued row).
        services.TryAddScoped<AgentSmith.Infrastructure.Persistence.Repositories.InitRunRepository>();
        services.AddScoped<AgentSmith.Server.Services.Branches.WorkBranchCleanupLauncher>();
        services.AddSingleton<AgentSmith.Server.Services.Branches.WorkBranchCleanupHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentSmith.Server.Services.Branches.WorkBranchCleanupHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp => sp.GetRequiredService<AgentSmith.Server.Services.Branches.WorkBranchCleanupHostedService>().Health);

        // p0281a: keep the connection repo snapshot warm (warm-on-start + interval).
        services.AddHostedService<RepoDiscoveryRefreshHostedService>();

        // p0358: a skills.version change on config reload pulls + logs immediately
        // (every replica — the skills cache is per-pod, unlike the leader-only pollers).
        services.AddHostedService<SkillsCatalogReloadHostedService>();

        // 2026-08-25-1806: the config store's assembled document is per-pod, so a save on
        // one replica has to reach the others for a role mapping to apply without a restart.
        services.AddHostedService<ConfigStoreReloadHostedService>();

        services.AddSingleton<RedisConnectionHealth>();
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<RedisConnectionHealth>().Health);

        return services;
    }
}
