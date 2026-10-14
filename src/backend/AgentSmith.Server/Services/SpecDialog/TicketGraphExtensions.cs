using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-10-14-dbcek: what a proposed list of tickets needs from the server — the existence read its review
/// and its filing share.</summary>
internal static class TicketGraphExtensions
{
    internal static IServiceCollection AddTicketGraph(this IServiceCollection services) =>
        services.AddScoped<ITicketExistence, TicketExistence>();
}
