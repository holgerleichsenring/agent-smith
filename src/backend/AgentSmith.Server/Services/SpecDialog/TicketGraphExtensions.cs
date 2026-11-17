using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using AgentSmith.Application.Services.Filing;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-10-14-dbcek: what a proposed list of tickets needs from the server — the existence read its review
/// and its filing share. 2026-11-17-5701b: the filing itself is the 'file' pipeline's (AddFilingPipeline).</summary>
internal static class TicketGraphExtensions
{
    internal static IServiceCollection AddTicketGraph(this IServiceCollection services)
    {
        services.AddScoped<ITicketExistence, TicketExistence>();
        return services;
    }
}
