using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// p0320c: background-service wrapper that runs <see cref="CapacityQueuePump"/>
/// under leader election (lease key 'agentsmith:leader:capacity-queue') with the
/// redis-gated retry loop — mirrors <see cref="HousekeepingLeaderHostedService"/>.
/// One pump across all replicas: the claim path's lease/lock still guard a race,
/// but a single dequeuer keeps the tracker re-validation load at one ticket/tick.
/// </summary>
public sealed class CapacityQueuePumpHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<CapacityQueuePumpHostedService> logger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:capacity-queue";
    private readonly SubsystemHealth _health = new("capacity_queue");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "CapacityQueuePumpHostedService.ExecuteAsync entered (lease key: {Key})", LeaseKey);
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(
            services, _health, LeaseKey, RunPumpAsync, retry, stoppingToken);
    }

    private Task RunPumpAsync(CancellationToken ct)
    {
        var pump = new CapacityQueuePump(
            services.GetRequiredService<ICapacityQueue>(),
            services.GetRequiredService<ITicketClaimService>(),
            services.GetRequiredService<ITicketProviderFactory>(),
            services.GetRequiredService<ISandboxResourceResolver>(),
            services.GetRequiredService<IOrchestratorResourceResolver>(),
            services.GetRequiredService<ISandboxCapacityProbe>(),
            services.GetRequiredService<IEventPublisher>(),
            services.GetRequiredService<IRunCancelStateReader>(),
            configLoader, serverContext.ConfigPath,
            services.GetRequiredService<ILogger<CapacityQueuePump>>());
        return pump.RunAsync(ct);
    }
}
