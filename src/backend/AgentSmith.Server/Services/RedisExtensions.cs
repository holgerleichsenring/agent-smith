using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
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
        services.AddSingleton<IConnectionMultiplexer>(
            sp => Connect(sp.GetRequiredService<IStartupFindings>()));
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
        // p0403: the envelope codec is a service both publisher and reader share.
        services.AddSingleton<AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer>();
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
    //
    // p0391b: AbortOnConnectFail only covers an endpoint that is DOWN. A REDIS_URL that
    // does not parse, or that parses to no endpoint at all (REDIS_URL=""), still threw —
    // out of a lazy factory resolved from three unguarded places, so a typo in one env-var
    // killed the server. An unusable URL is now a finding plus the default endpoint, which
    // simply never connects: the queue stays idle and RedisProbe names the cause.
    private static IConnectionMultiplexer Connect(IStartupFindings findings)
    {
        var options = ParseOptions(findings);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    }

    private static ConfigurationOptions ParseOptions(IStartupFindings findings)
    {
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? DispatcherDefaults.RedisUrl;
        try
        {
            var parsed = ConfigurationOptions.Parse(redisUrl);
            if (parsed.EndPoints.Count > 0) return parsed;
            findings.Record(Unusable(redisUrl, "it names no endpoint"));
        }
        catch (Exception ex)
        {
            findings.Record(Unusable(redisUrl, ex.Message));
        }
        return ConfigurationOptions.Parse(DispatcherDefaults.RedisUrl);
    }

    private static StartupFinding Unusable(string redisUrl, string reason) => new(
        StartupSubsystems.Redis,
        StartupFindingSeverity.Blocking,
        $"REDIS_URL '{redisUrl}' cannot be used as a Redis endpoint ({reason}), so the queue, "
        + $"the leader lease and the event stream stay down. Falling back to "
        + $"'{DispatcherDefaults.RedisUrl}'. Expected form: host:port.",
        Field: "REDIS_URL");
}
