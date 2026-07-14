using AgentSmith.Application;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Factories;
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
                sp.GetRequiredService<ILogger<TicketAwarePipelineLifecycleCoordinator>>()));
        // p0246b: the single-run lease default is the no-op (DB-free) binding so
        // the Redis heartbeat / status transition stay the guard. AddRelational
        // Persistence (opt-in, when persistence is configured) swaps in the
        // DB-backed DbActiveRunLease whose UNIQUE(Project,TicketId) index becomes
        // the authoritative guard. Registered before TicketClaimService so the
        // claim service resolves it.
        services.AddSingleton<IActiveRunLease, NoOpActiveRunLease>();
        // p0320c: same shape for the capacity queue — the no-op default keeps a
        // DB-free composition on the stateless defer-and-retry path; AddRelational
        // Persistence swaps in the DbCapacityQueue (persistent FIFO + queued rows).
        services.AddSingleton<ICapacityQueue, AgentSmith.Application.Services.Spawning.NoOpCapacityQueue>();
        // p0336: the run-footprint calculator (remote inventory → full pod footprint)
        // and the capacity budget. The no-op budget is the DB-free default (admits
        // unconditionally); AddRelationalPersistence swaps in the DB-backed ledger.
        services.AddTransient<IRunFootprintCalculator, RunFootprintCalculator>();
        services.AddSingleton<ICapacityBudget, NoOpCapacityBudget>();
        // ITicketClaimService is stateless; its deps (IRedisClaimLock,
        // ITicketStatusTransitionerFactory, IRedisJobQueue) are all singletons. Singleton
        // lifetime keeps the singleton WebhookSpawnDispatcher dependency chain valid.
        services.AddSingleton<ITicketClaimService, TicketClaimService>();
        // Server-only: the webhook/poller fan-out at enqueue. Depends on
        // ITicketClaimService above, so it lives here, not in the shared
        // AddPipelineExecution (where it could not be constructed for the CLI).
        services.AddTransient<ISpawnPipelineRunsUseCase,
            AgentSmith.Application.Services.Spawning.SpawnPipelineRunsUseCase>();
        return services;
    }
}
