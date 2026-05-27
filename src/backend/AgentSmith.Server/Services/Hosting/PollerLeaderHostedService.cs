using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Background-service wrapper that runs <see cref="PollerHostedService"/>
/// under leader election (lease key 'agentsmith:leader:poller') with the
/// redis-gated retry loop.
/// </summary>
public sealed class PollerLeaderHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<PollerLeaderHostedService> logger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:poller";
    private readonly SubsystemHealth _health = new("poller");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PollerLeaderHostedService.ExecuteAsync entered (lease key: {Key})", LeaseKey);
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(
            services, _health, LeaseKey, RunPollerAsync, retry, stoppingToken);
    }

    private async Task RunPollerAsync(CancellationToken ct)
    {
        logger.LogInformation("RunPollerAsync entered — building per-tracker pollers");
        using var scope = services.CreateScope();
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        logger.LogInformation(
            "Building pollers from {TrackerCount} trackers (polling-enabled subset)", config.Trackers.Count);
        var host = new PollerHostedService(
            PollerFactory.Build(services, config),
            services.GetRequiredService<AgentSmith.Contracts.Events.ISystemEventPublisher>(),
            services.GetRequiredService<ILogger<PollerHostedService>>());
        logger.LogInformation("Handing control to PollerHostedService.RunAsync");
        await host.RunAsync(ct);
        logger.LogInformation("PollerHostedService.RunAsync returned (cancellation or lease loss)");
    }
}
