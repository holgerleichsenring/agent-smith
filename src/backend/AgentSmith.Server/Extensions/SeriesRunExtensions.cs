using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.Series;

namespace AgentSmith.Server.Extensions;

/// <summary>2026-11-17-0207a: the series run — its steps, the ports they read and act through, and the sweep that beats
/// it on the housekeeping leader.</summary>
public static class SeriesRunExtensions
{
    public static IServiceCollection AddSeriesRuns(this IServiceCollection services)
    {
        services.AddSeriesRunPipeline();
        services.AddScoped<SeriesParkedQuestion>().AddScoped<ISeriesTicketFacts, SeriesTicketFacts>();
        services.AddScoped<SeriesTicketMessage>().AddScoped<ISeriesTicketActs, SeriesTicketActs>();
        services.AddScoped<ISeriesConversation, SeriesConversation>().AddScoped<ISeriesRunLaunch, SeriesRunLaunch>();
        services.AddSingleton<SeriesRunSweeper>();
        return services;
    }
}
