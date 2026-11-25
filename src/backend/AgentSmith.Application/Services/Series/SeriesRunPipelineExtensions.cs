using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the series run's steps. Called by the server's composition only — the steps need the
/// series store, the ticket facts and the acts a server registers.</summary>
public static class SeriesRunPipelineExtensions
{
    public static IServiceCollection AddSeriesRunPipeline(this IServiceCollection services)
    {
        services.AddScoped<SeriesBeatDemand>().AddScoped<SeriesQuestions>().AddScoped<ISeriesCriteria, SeriesCriteria>().AddScoped<SeriesProgress>();
        services.AddScoped<SeriesPause>(); // 2026-11-19-1b72a: the master's pause and the head's buttons
        services.AddScoped<SeriesBudgetQuestion>(); // 2026-11-25-9992g
        services.AddTransient<SeriesRunOrder>(); // 2026-11-23-31dcb: depth and merge order of the series page
        services.AddScoped<SeriesMembership>(); // 2026-11-24-f9c5b: the filing's series, written before its tickets start
        services.AddTransient<ICommandHandler<VerifySeriesContext>, VerifySeriesHandler>(); // 0207d
        services.AddTransient<ICommandHandler<OpenSeriesRunContext>, OpenSeriesRunHandler>();
        services.AddTransient<ICommandHandler<ReadSeriesContext>, ReadSeriesHandler>();
        services.AddTransient<SeriesFinalMerge>().AddTransient<SeriesFinalPullRequest>() // 2026-11-24-f9c5e
            .AddTransient<SeriesFinalCi>() // 2026-11-25-9992e: the master's CI act on the final pull requests
            .AddTransient<ICommandHandler<DeliverSeriesContext>, DeliverSeriesHandler>();
        services.AddTransient<ICommandHandler<SeriesMasterContext>, SeriesMasterHandler>();
        services.AddTransient<ICommandHandler<PostSeriesStatusContext>, PostSeriesStatusHandler>();
        return services;
    }
}
