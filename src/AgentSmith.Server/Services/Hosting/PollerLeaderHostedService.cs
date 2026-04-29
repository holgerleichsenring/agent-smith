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
    ILogger<LeaderElectedHostedService> leaderLogger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:poller";
    private readonly SubsystemHealth _health = new("poller");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(
            services, _health, LeaseKey, RunPollerAsync, retry, stoppingToken);
    }

    private Task RunPollerAsync(CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        var host = new PollerHostedService(
            PollerFactory.Build(services, config),
            services.GetRequiredService<ITicketClaimService>(),
            configLoader, serverContext.ConfigPath,
            services.GetRequiredService<ILogger<PollerHostedService>>());
        return host.RunAsync(ct);
    }
}
