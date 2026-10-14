using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59n: Retry — the runs' gate over the ticket readers, and the request that records and nudges.
/// Chained from the dashboard API, which maps the route, and from the ticket conversation, which asks from a
/// turn — so every registration is a TryAdd, and the readers the gate needs come with it.
/// </summary>
internal static class RunRetryExtensions
{
    internal static IServiceCollection AddRunRetry(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<TicketAttempts>();
        services.TryAddTransient<TicketRunsQuery>();
        services.TryAddTransient<InfrastructureEnd>();
        services.TryAddTransient<TicketLaunchEnds>();
        services.TryAddTransient<RunRetryGate>();
        services.TryAddScoped<RunRetry>();
        return services;
    }
}
