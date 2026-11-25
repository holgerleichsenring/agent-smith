using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.Series;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>2026-11-17-0207a: the series run — its steps, the ports they read and act through, and the sweep that beats
/// it on the housekeeping leader.</summary>
public static class SeriesRunExtensions
{
    public static IServiceCollection AddSeriesRuns(this IServiceCollection services)
    {
        services.AddSeriesRunPipeline();
        services.AddScoped<SeriesParkedQuestion>().AddScoped<SeriesStatusReader>() // 2026-11-25-9992a: the one state
            .AddScoped<ISeriesTicketFacts>(sp => sp.GetRequiredService<SeriesStatusReader>());
        services.TryAddSingleton<Services.Preconditions.InterruptedRestartWindow>(); // 2026-11-22-6d85c
        services.AddScoped<SeriesTicketMessage>().AddScoped<SeriesRunCancel>().AddScoped<ISeriesTicketActs, SeriesTicketActs>();
        services.AddScoped<ISeriesConversation, SeriesConversation>().AddScoped<ISeriesRunLaunch, SeriesRunLaunch>();
        services.AddScoped<ISeriesRecordReads, SeriesRecordReads>(); // 0207e: the admin assistant's run reads, lent
        services.AddSingleton<SeriesRunSweeper>().AddScoped<SeriesRunViewReader>(); // 0207e: the panel's read
        services.TryAddTransient<SeriesRunPullRequests>(); // 2026-11-23-31dcb: the series page's reads
        services.TryAddTransient<SeriesRunTickets>();
        services.TryAddScoped<SeriesRunNames>();
        services.AddScoped<SeriesRunCosts>(); // 2026-11-23-31dcd
        services.AddScoped<SeriesMergeNextAct>(); // 2026-11-23-31dce: the series page's merge
        services.TryAddScoped<SeriesRunProjectReads>();
        services.AddTransient<SeriesRunHolds>(); // 2026-11-23-31dcg
        services.AddTransient<SeriesRunChangesReader>(); // 2026-11-23-31dcj: read when Changed in this series opens
        services.AddScoped<SeriesRunOverviewReader>().AddScoped<SeriesRunDetailReader>();
        return services;
    }
}
