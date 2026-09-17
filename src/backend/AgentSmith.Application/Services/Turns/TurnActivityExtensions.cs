using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// Registers the ambient observer a turn may set to hear its own steps.
/// </summary>
public static class TurnActivityExtensions
{
    public static IServiceCollection AddTurnActivity(this IServiceCollection services)
    {
        // One instance per process is right: the observer lives in the async flow, not here.
        services.TryAddSingleton<ITurnActivityObserverAccessor, AsyncLocalTurnActivityObserverAccessor>();
        services.TryAddSingleton<TurnActivityTools>();
        return services;
    }
}
