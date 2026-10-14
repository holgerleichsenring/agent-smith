using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-10-14-dbcek: what a proposed list of tickets needs from the server — the existence read its review
/// and its filing share. 2026-10-14-dbcel: and the filing — the filer, its start pass and the ticket-text record. 2026-10-14-dbcem: and its tracker links.</summary>
internal static class TicketGraphExtensions
{
    internal static IServiceCollection AddTicketGraph(this IServiceCollection services)
    {
        services.AddScoped<ITicketExistence, TicketExistence>();
        services.AddScoped<TicketGraphFiler>();
        services.AddScoped<TicketGraphStarts>();
        services.AddScoped<GraphTicketApproval>();
        services.AddScoped<TicketGraphLinks>();
        return services;
    }
}
