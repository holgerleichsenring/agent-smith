using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Background-service wrapper that runs <see cref="PollerHostedService"/>
/// under leader election (lease key 'agentsmith:leader:poller') with the
/// redis-gated retry loop. p0353: the pollers are rebuilt in place when the config epoch advances
/// (<see cref="LeaderEpochLoop"/>, shared with the change sweep since 2026-10-08-9e6e).
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

    private Task RunPollerAsync(CancellationToken ct) =>
        services.GetRequiredService<LeaderEpochLoop>().RunAsync("poller", (config, token) =>
        {
            logger.LogInformation("Building pollers from {TrackerCount} trackers", config.Trackers.Count);
            return new PollerHostedService(
                PollerFactory.Build(services, config),
                services.GetRequiredService<AgentSmith.Contracts.Events.ISystemEventPublisher>(),
                services.GetRequiredService<ILogger<PollerHostedService>>()).RunAsync(token);
        }, ct);
}
