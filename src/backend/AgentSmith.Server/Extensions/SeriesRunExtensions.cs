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
        services.AddScoped<SeriesTicketMessage>().AddScoped<SeriesTicketAnswer>().AddScoped<SeriesRunCancel>().AddScoped<ISeriesTicketActs, SeriesTicketActs>();
        services.AddScoped<ISeriesConversation, SeriesConversation>().AddScoped<ISeriesRunLaunch, SeriesRunLaunch>();
        services.AddScoped<ISeriesRecordReads, SeriesRecordReads>(); // 0207e: the admin assistant's run reads, lent
        services.AddTransient<ISeriesSourceScopes, SeriesSourceScopes>(); // 2026-11-27-3532c: the repositories and uploads it reads
        services.AddSingleton<SeriesRunSweeper>().AddScoped<SeriesRunViewReader>(); // 0207e: the panel's read
        services.AddSeriesWakes();
        services.TryAddTransient<SeriesRunPullRequests>(); // 2026-11-23-31dcb: the series page's reads
        services.TryAddTransient<SeriesRunTickets>();
        services.TryAddScoped<SeriesRunNames>();
        services.AddScoped<SeriesRunCosts>(); // 2026-11-23-31dcd
        services.AddScoped<Services.Measurement.ModelMeasurementReader>(); // 2026-11-25-9992k
        services.AddScoped<SeriesMergeNextAct>(); // 2026-11-23-31dce: the series page's merge
        services.AddScoped<SeriesTicketCi>().AddScoped<ISeriesPullRequestActs, SeriesPullRequestActs>(); // 2026-11-25-9992e: the master's
        services.AddScoped<SeriesRequirementLinks>().AddScoped<ISeriesEditActs, SeriesEditActs>(); // 2026-11-25-9992f: the master's cut
        services.AddScoped<SeriesAddedSpec>(); // 2026-11-27-3532a: add_ticket files a spec
        services.TryAddScoped<SeriesRunProjectReads>();
        services.AddTransient<SeriesRunHolds>(); // 2026-11-23-31dcg
        services.AddTransient<SeriesRunChangesReader>(); // 2026-11-23-31dcj: read when Changed in this series opens
        services.AddScoped<SeriesRunOverviewReader>().AddScoped<SeriesRunDetailReader>();
        return services;
    }

    /// <summary>2026-11-27-3532b: the resume sweep's fallback answers — the ticket's comment and the deadline default, none for
    /// a series-led run (<see cref="SeriesLedRun"/>, which the answer endpoint reads too), whose passed deadline wakes its master.</summary>
    public static IServiceCollection AddParkedAnswerFallback(this IServiceCollection services)
    {
        services.AddSeriesWakes().TryAddSingleton<SeriesLedRun>();
        services.TryAddSingleton<Services.Lifecycle.SeriesQuestionDeadline>();
        services.TryAddSingleton<Services.Lifecycle.ParkedAnswerFallback>();
        return services;
    }

    /// <summary>2026-11-25-9992g: what wakes the master, recorded — by the PR read, the run end, the tracker and the PR
    /// comments, so the worker's and the webhooks' compositions each take it in.</summary>
    public static IServiceCollection AddSeriesWakes(this IServiceCollection services)
    {
        services.TryAddSingleton<ISeriesWakes, SeriesWakes>();
        return services;
    }
}
