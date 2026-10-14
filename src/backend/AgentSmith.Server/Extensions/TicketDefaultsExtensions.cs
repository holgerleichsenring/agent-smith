using AgentSmith.Server.Services.Tickets.Defaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-8762p: a ticket's defaults — the read over the ticket readers and the batch writer that decides
/// them. Chained from the dashboard API, the only place the routes are mapped.
/// </summary>
internal static class TicketDefaultsExtensions
{
    internal static IServiceCollection AddTicketDefaults(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services.AddTransient<TicketDefaultsReader>().AddTransient<DefaultVerdictValidation>()
            .AddTransient<DefaultVerdictWriter>();
    }
}
