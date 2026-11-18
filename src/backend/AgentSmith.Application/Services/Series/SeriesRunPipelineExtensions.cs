using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the series run's steps. Called by the server's composition only — the steps need the
/// series store, the ticket facts and the acts a server registers.</summary>
public static class SeriesRunPipelineExtensions
{
    public static IServiceCollection AddSeriesRunPipeline(this IServiceCollection services)
    {
        services.AddScoped<SeriesBeatDemand>();
        services.AddTransient<ICommandHandler<OpenSeriesRunContext>, OpenSeriesRunHandler>();
        services.AddTransient<ICommandHandler<ReadSeriesContext>, ReadSeriesHandler>();
        services.AddTransient<ICommandHandler<SeriesMasterContext>, SeriesMasterHandler>();
        services.AddTransient<ICommandHandler<PostSeriesStatusContext>, PostSeriesStatusHandler>();
        return services;
    }
}
