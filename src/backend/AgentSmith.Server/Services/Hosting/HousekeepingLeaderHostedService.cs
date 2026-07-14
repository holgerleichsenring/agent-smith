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
        // p0262: StaleJobDetector is GONE. It reverted a stale in-progress LABEL — but
        // tags are now pure markers and the native status + lease decide. Its only
        // load-bearing job (cancel the run of a stale lease) moved into ActiveRunReaper,
        // which already releases the lease. Recovery of a dead run is now entirely the
        // reaper's: cancel + release → the ticket (still natively open) is reclaimed.
        logger.LogInformation(
            "RunHousekeepingAsync entered — EnqueuedReconciler + PipelineRunWatchdog + CancelEnforcer + DialogueResumeSweeper");
        var queue = services.GetRequiredService<IRedisJobQueue>();
        var ticketFactory = services.GetRequiredService<ITicketProviderFactory>();
        var activeRunLease = services.GetRequiredService<IActiveRunLease>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var reconciler = new EnqueuedReconciler(
            activeRunLease, queue, ticketFactory, configLoader,
            services.GetRequiredService<IEnvelopeProjectResolver>(), timeProvider, serverContext.ConfigPath,
            services.GetRequiredService<ILogger<EnqueuedReconciler>>());
        var watchdog = BuildWatchdog();
        // p0330: the durable cancel guarantee — leader-elected like the rest of
        // housekeeping so one replica enforces kill deadlines.
        var enforcer = services.GetRequiredService<AgentSmith.Server.Services.Lifecycle.CancelEnforcer>();
        // p0327: answered/expired dialogue checkpoints re-enter through the
        // capacity queue — leader-elected so one replica sweeps.
        var resumeSweeper = services.GetRequiredService<AgentSmith.Server.Services.Lifecycle.DialogueResumeSweeper>();
        return Task.WhenAll(
            reconciler.RunAsync(ct), watchdog.RunAsync(ct), enforcer.RunAsync(ct), resumeSweeper.RunAsync(ct));
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
