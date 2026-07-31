using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.Persistence;
using AgentSmith.Infrastructure.Services.Queue;
using AgentSmith.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // Lazy factory — connect on first resolve, NOT at registration time.
        // Eager Connect() here broke CI for the PipelineHarness fast tier:
        // ServerCompositionBuilder.ConfigureServices is called during test
        // setup and tried to reach Redis at localhost:6379 before any handler
        // was even resolved. Production behaviour unchanged: Server hosted
        // services resolve Redis-dependent singletons at startup, so the
        // connection still happens immediately on real start; fast-tier
        // tests that never touch Redis-backed services now don't trip it.
        services.AddSingleton<IConnectionMultiplexer>(_ => Connect());
        services.AddSingleton<IRedisJobQueue, RedisJobQueue>();
        services.AddSingleton<IRedisClaimLock, RedisClaimLock>();
        services.AddSingleton<IRedisLeaderLease, RedisLeaderLease>();
        services.AddSingleton<IConfigReloadSignal, RedisConfigReloadSignal>(); // p0353
        services.AddSingleton<IConversationLookup, RedisConversationLookup>();
        services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
        services.AddSingleton<IRunArtifactStore, RedisRunArtifactStore>();
        // p0388a: the Redis publisher is the transport; the step-attributing
        // decorator in front of it is the single place the ambient step scope is
        // stamped onto every event, so no emit site plumbs a step index.
        services.AddSingleton<RedisEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => new StepAttributingEventPublisher(
            sp.GetRequiredService<RedisEventPublisher>(),
            sp.GetRequiredService<IRunContextAccessor>()));
        services.AddSingleton<ISystemEventPublisher, RedisSystemEventPublisher>();
        // p0182: ProjectMap cache moves to Redis so analyzer cost survives
        // container restart. Replaces any prior IProjectMapStore registration
        // from the CLI-safe baseline (disk-backed) registered upstream.
        services.RemoveAll<IProjectMapStore>();
        services.AddSingleton<IProjectMapStore, RedisProjectMapStore>();
        return services;
    }

    // p0391a: AbortOnConnectFail=false. Every Redis-backed hosted service takes the
    // multiplexer by constructor, and the host resolves ALL hosted services before it
    // starts any — so an aborting Connect() threw out of host start and killed a server
    // that could otherwise have said "Redis is down". Now the multiplexer comes up in a
    // reconnecting state, the queue stays idle, and RedisProbe names the cause.
    private static IConnectionMultiplexer Connect()
    {
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? DispatcherDefaults.RedisUrl;
        var options = ConfigurationOptions.Parse(redisUrl);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    }
}
