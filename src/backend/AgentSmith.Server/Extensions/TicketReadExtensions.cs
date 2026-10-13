using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59a: the ticket read model — computed over the runs on every request, nothing
/// stored. Transient like the filed-work runs reader it composes attempts through; each read opens
/// its own scope for the unit of work.
/// </summary>
internal static class TicketReadExtensions
{
    internal static IServiceCollection AddTicketReaders(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<TicketAttempts>()
            .AddTransient<InfrastructureEnd>()
            .AddTransient<TicketAttemptMarker>()
            .AddTransient<TicketBucketRule>()
            .AddTransient<TicketKeyResolver>()
            .AddTransient<TicketLiveStateReader>()
            .AddTransient<TicketSummaryComposer>()
            .AddTransient<TicketListQuery>()
            .AddTransient<TicketSpecStatuses>()
            .AddTransient<TicketMonthCost>()
            .AddTransient<TicketCounts>()
            .AddTransient<TicketListReader>()
            .AddTransient<TicketDetailReader>()
            .AddTransient<TicketReviewState>() // 2026-10-13-9a59b
            .AddTransient<TicketPullRequestReader>()
            .AddTransient<TicketAttemptRecords>() // 2026-10-13-9a59d
            .AddTransient<TicketRunsQuery>() // 2026-10-13-9a59e
            .AddTransient<TicketSeriesRecordsReader>()
            .AddTransient<TicketSeriesSpecComposer>()
            .AddTransient<TicketSeriesReader>()
            .AddTransient<SeriesStripReader>();
        return services;
    }
}
