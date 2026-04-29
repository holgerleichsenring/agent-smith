using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Background-service wrapper that runs <see cref="PipelineQueueConsumer"/>
/// behind <see cref="SubsystemTask"/>'s redis-gated retry loop.
/// </summary>
public sealed class QueueConsumerHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<PipelineQueueConsumer> logger) : BackgroundService
{
    private readonly SubsystemHealth _health = new("queue_consumer");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("QueueConsumerHostedService.ExecuteAsync entered");
        var config = configLoader.LoadConfig(serverContext.ConfigPath).Queue;
        return SubsystemTask.RunRedisGatedAsync<IRedisJobQueue>(
            services, _health, config.RedisRetryIntervalSeconds,
            (queue, ct) => new PipelineQueueConsumer(
                services, queue, serverContext.ConfigPath,
                config.MaxParallelJobs, config.ShutdownGraceSeconds, logger).RunAsync(ct),
            logger, stoppingToken);
    }
}
