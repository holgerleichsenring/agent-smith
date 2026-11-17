using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<SeriesFingerprintStamp>().AddScoped<TicketBodyRender>(); // 2026-11-17-dd05b
        services.TryAddTransient<SeriesManifest>(); services.TryAddTransient<SeriesFiles>(); services.TryAddTransient<SeriesRepos>();
        services.TryAddTransient<SpecTicketRenderer>(); services.TryAddTransient<SeriesTicketRegion>(); services.TryAddTransient<SpecSetPointerRecorder>();
        services.AddTransient<ICommandHandler<RenderTicketBodyContext>, RenderTicketBodyHandler>();
        services.AddTransient<ICommandHandler<RetargetPullRequestsContext>, RetargetPullRequestsHandler>(); // 2026-11-17-9a39c
        return services;
    }
}
