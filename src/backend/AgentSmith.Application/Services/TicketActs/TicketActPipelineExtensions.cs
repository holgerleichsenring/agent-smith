using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-17-e061a: the steps of the runs that act on a filed ticket and the services they share. Called by the
/// server's composition only, as AddFilingPipeline is: the close's failure classifier is the server's.
/// </summary>
public static class TicketActPipelineExtensions
{
    public static IServiceCollection AddTicketActPipelines(this IServiceCollection services)
    {
        services.AddScoped<TicketHostClose>().AddScoped<TicketCloseActs>();
        services.AddTransient<ICommandHandler<CloseTicketContext>, CloseTicketHandler>();
        services.AddTransient<ICommandHandler<ReopenTicketContext>, ReopenTicketHandler>();
        services.AddTransient<ICommandHandler<StartTicketContext>, StartTicketHandler>(); // 2026-11-17-e061b
        services.AddScoped<TicketDescriptionRewrite>().AddScoped<TicketAmendmentWrite>(); // 2026-11-17-e061c
        services.AddTransient<ICommandHandler<AmendTicketContext>, AmendTicketHandler>();
        services.AddTransient<ICommandHandler<PostTicketCommentsContext>, PostTicketCommentsHandler>();
        return services;
    }
}
