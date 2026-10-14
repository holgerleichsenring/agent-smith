using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59n: Retry — the runs' gate over the ticket readers, and the request that records and nudges.
/// Chained from the dashboard API, the only place the route is mapped.
/// </summary>
internal static class RunRetryExtensions
{
    internal static IServiceCollection AddRunRetry(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services.AddTransient<RunRetryGate>().AddScoped<RunRetry>();
    }
}
