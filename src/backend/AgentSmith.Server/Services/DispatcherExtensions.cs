using AgentSmith.Application;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Lifecycle;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Server-side dispatcher composition: in-memory message bus + state managers,
/// the bus message router + listener (hosted), the orphan-job detector (hosted),
/// then chains the Application + Infrastructure compositions and the IntentEngine.
/// AddServerCompositionOverrides supplies the last-wins bindings that flip
/// ITicketStatusTransitionerFactory to the locking variant for Jira,
/// IPipelineLifecycleCoordinator to the ticket-aware variant with heartbeat support,
/// and registers ITicketClaimService (Server-only since p0109a).
/// </summary>
internal static class DispatcherExtensions
{
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
        // ITicketClaimService is stateless; its deps (IRedisClaimLock,
        // ITicketStatusTransitionerFactory, IRedisJobQueue) are all singletons. Singleton
        // lifetime keeps the singleton WebhookSpawnDispatcher dependency chain valid.
        services.AddSingleton<ITicketClaimService, TicketClaimService>();
        return services;
    }
}
