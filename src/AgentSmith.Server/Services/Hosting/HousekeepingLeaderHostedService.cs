using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Background-service wrapper that runs StaleJobDetector + EnqueuedReconciler
/// under leader election (lease key 'agentsmith:leader:housekeeping') with the
/// redis-gated retry loop.
/// </summary>
public sealed class HousekeepingLeaderHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<LeaderElectedHostedService> leaderLogger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:housekeeping";
    private readonly SubsystemHealth _health = new("housekeeping");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(
            services, _health, LeaseKey, RunHousekeepingAsync, retry, stoppingToken);
    }

    private Task RunHousekeepingAsync(CancellationToken ct)
    {
        var heartbeat = services.GetRequiredService<IJobHeartbeatService>();
        var queue = services.GetRequiredService<IRedisJobQueue>();
        var ticketFactory = services.GetRequiredService<ITicketProviderFactory>();
        var transitionerFactory = services.GetRequiredService<ITicketStatusTransitionerFactory>();
        var stale = new StaleJobDetector(
            heartbeat, ticketFactory, transitionerFactory, configLoader, serverContext.ConfigPath,
            services.GetRequiredService<ILogger<StaleJobDetector>>());
        var reconciler = new EnqueuedReconciler(
            heartbeat, queue, ticketFactory, configLoader,
            services.GetRequiredService<IPipelineConfigResolver>(), serverContext.ConfigPath,
            services.GetRequiredService<ILogger<EnqueuedReconciler>>());
        return Task.WhenAll(stale.RunAsync(ct), reconciler.RunAsync(ct));
    }
}
