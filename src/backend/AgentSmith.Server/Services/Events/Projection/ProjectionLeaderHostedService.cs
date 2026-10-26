using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: runs <see cref="ProjectionLeader"/> under the lease key
/// <see cref="ProjectionLeader.LeaseKey"/>, the way the other single-replica subsystems run. A server with
/// no relational store has nothing to write and holds no lease.
/// </summary>
public sealed class ProjectionLeaderHostedService(
    IServiceProvider services, ServerContext serverContext, IConfigurationLoader configLoader) : BackgroundService
{
    private readonly SubsystemHealth _health = new("projection");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (services.GetService<RunDbProjector>() is null) return Task.CompletedTask;
        var leader = services.GetRequiredService<ProjectionLeader>();
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(services, _health, ProjectionLeader.LeaseKey, leader.LeadAsync, retry, stoppingToken);
    }
}
