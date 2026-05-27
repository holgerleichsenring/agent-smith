using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.Lifecycle;
using AgentSmith.Infrastructure.Services.Persistence;
using AgentSmith.Infrastructure.Services.Queue;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Redis composition: connects the multiplexer once and registers the queue,
/// claim-lock, leader-lease, heartbeat, conversation-lookup, dialogue-transport,
/// and run-artifact-store services. Server adds these onto the CLI-safe baseline
/// Application + Infrastructure registered.
/// </summary>
internal static class RedisExtensions
{
    internal static IServiceCollection AddRedis(this IServiceCollection services)
    {
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? DispatcherDefaults.RedisUrl;
        var multiplexer = ConnectionMultiplexer.Connect(redisUrl);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<IRedisJobQueue, RedisJobQueue>();
        services.AddSingleton<IRedisClaimLock, RedisClaimLock>();
        services.AddSingleton<IRedisLeaderLease, RedisLeaderLease>();
        services.AddSingleton<IJobHeartbeatService, JobHeartbeatService>();
        services.AddSingleton<IConversationLookup, RedisConversationLookup>();
        services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
        services.AddSingleton<IRunArtifactStore, RedisRunArtifactStore>();
        services.AddSingleton<IEventPublisher, RedisEventPublisher>();
        services.AddSingleton<ISystemEventPublisher, RedisSystemEventPublisher>();
        return services;
    }
}
