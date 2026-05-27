using AgentSmith.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Registers the NoOp publisher as the safe default + the ambient run-context
/// accessor used by cross-cutting decorators. Server's AddRedis swaps in
/// <c>RedisEventPublisher</c>; the last-write-wins overrides the
/// TryAddSingleton baseline. CLI / tests see NoOp without further wiring.
/// </summary>
public static class EventPublishingExtensions
{
    public static IServiceCollection AddEventPublishing(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventPublisher, NoOpEventPublisher>();
        services.TryAddSingleton<IRunContextAccessor, AsyncLocalRunContextAccessor>();
        // p0173a: parallel system-event channel. NoOp is the safe default;
        // Server's AddRedis swaps in RedisSystemEventPublisher via last-write-
        // wins over TryAddSingleton.
        services.TryAddSingleton<ISystemEventPublisher, NoOpSystemEventPublisher>();
        return services;
    }
}
