using AgentSmith.Application;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Lifecycle;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Server-side DI extensions. Helpers are split into partial files by subdomain
/// (Redis, Webhooks, LongRunning, Intent, PlatformAdapters, JobSpawner) so each
/// file stays under the 120-line limit. The dispatcher composition entry point
/// + the Server overrides (which depend on Application + Infrastructure being
/// registered first) live here.
/// </summary>
internal static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Bundles the Server-side overrides on top of Application's CLI-safe defaults:
    /// (1) ITicketStatusTransitionerFactory rebinds to the locking variant for Jira;
    /// (2) IPipelineLifecycleCoordinator rebinds to the ticket-aware variant with
    ///     heartbeat support;
    /// (3) ITicketClaimService is registered (Server-only — depends on Redis services
    ///     that the CLI never carries).
    /// Must be called AFTER AddCoreDispatcherServices so the last-wins overrides
    /// stick against the bindings AddAgentSmithInfrastructure / AddAgentSmithCommands
    /// established.
    /// </summary>
    internal static IServiceCollection AddServerCompositionOverrides(this IServiceCollection services)
    {
        services.AddSingleton<ITicketStatusTransitionerFactory>(sp =>
            new LockingTicketStatusTransitionerFactory(
                sp.GetRequiredService<TicketStatusTransitionerFactory>(),
                sp.GetRequiredService<IRedisClaimLock>(),
                sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IPipelineLifecycleCoordinator>(sp =>
            new TicketAwarePipelineLifecycleCoordinator(
                sp.GetRequiredService<ITicketStatusTransitionerFactory>(),
                sp.GetRequiredService<IJobHeartbeatService>(),
                sp.GetRequiredService<ILogger<TicketAwarePipelineLifecycleCoordinator>>()));
        // p0140b: ITicketClaimService is stateless; its deps (IRedisClaimLock,
        // ITicketStatusTransitionerFactory, IRedisJobQueue) are all singletons. Singleton
        // lifetime keeps the singleton WebhookSpawnDispatcher dependency chain valid.
        services.AddSingleton<ITicketClaimService, TicketClaimService>();
        return services;
    }

    internal static IServiceCollection AddCoreDispatcherServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        services.AddSingleton<ConversationStateManager>();
        services.AddSingleton<ClarificationStateManager>();
        services.AddSingleton<ChatIntentParser>();
        services.AddSingleton<IBusMessageRouter, BusMessageRouter>();
        services.AddSingleton<MessageBusListener>();
        services.AddHostedService(sp => sp.GetRequiredService<MessageBusListener>());
        services.AddHostedService<OrphanJobDetector>();
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddIntentEngine();
        return services;
    }
}
