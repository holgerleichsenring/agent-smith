using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ILogger<HousekeepingLeaderHostedService> logger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:housekeeping";
    private readonly SubsystemHealth _health = new("housekeeping");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HousekeepingLeaderHostedService.ExecuteAsync entered (lease key: {Key})", LeaseKey);
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(
            services, _health, LeaseKey, RunHousekeepingAsync, retry, stoppingToken);
    }

    private Task RunHousekeepingAsync(CancellationToken ct)
    {
        logger.LogInformation(
            "RunHousekeepingAsync entered — StaleJobDetector + EnqueuedReconciler + PipelineRunWatchdog");
        var queue = services.GetRequiredService<IRedisJobQueue>();
        var ticketFactory = services.GetRequiredService<ITicketProviderFactory>();
        var transitionerFactory = services.GetRequiredService<ITicketStatusTransitionerFactory>();
        var activeRunLease = services.GetRequiredService<IActiveRunLease>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var stale = new StaleJobDetector(
            ticketFactory, transitionerFactory,
            activeRunLease,
            services.GetRequiredService<IRunCancellationRegistry>(),
            services.GetRequiredService<IEventPublisher>(),
            timeProvider,
            configLoader, serverContext.ConfigPath,
            services.GetRequiredService<ILogger<StaleJobDetector>>());
        var reconciler = new EnqueuedReconciler(
            activeRunLease, queue, ticketFactory, configLoader,
            services.GetRequiredService<IPipelineConfigResolver>(), timeProvider, serverContext.ConfigPath,
            services.GetRequiredService<ILogger<EnqueuedReconciler>>());
        var watchdog = BuildWatchdog();
        return Task.WhenAll(stale.RunAsync(ct), reconciler.RunAsync(ct), watchdog.RunAsync(ct));
    }

    private PipelineRunWatchdog BuildWatchdog()
    {
        var registry = services.GetRequiredService<IRunCancellationRegistry>();
        var publisher = services.GetRequiredService<IEventPublisher>();
        var orchestrator = services
            .GetRequiredService<IOptions<OrchestratorGlobalConfig>>().Value;
        return new PipelineRunWatchdog(
            registry, publisher, orchestrator.MaxRunWallTimeSeconds,
            services.GetRequiredService<ILogger<PipelineRunWatchdog>>());
    }
}
