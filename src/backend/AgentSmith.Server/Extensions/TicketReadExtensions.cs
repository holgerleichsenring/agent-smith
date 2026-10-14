using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Checks;
using AgentSmith.Server.Services.Tickets.Conversation;
using AgentSmith.Server.Services.Tickets.Text;
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
        services.TryAddTransient<SpecDraftReader>();
        services.TryAddTransient<SpecRecordReader>();
        services.AddTransient<TicketAttempts>()
            .AddTransient<InfrastructureEnd>()
            .AddTransient<TicketLaunchEnds>() // 2026-10-13-9a59n
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
            .AddTransient<TicketSpecRecordsReader>()
            .AddTransient<TicketRunRecordVerdicts>()
            .AddTransient<TicketRunRecordSpecComposer>()
            .AddTransient<TicketRunRecordSeriesReader>()
            .AddTransient<SeriesStripReader>()
            .AddTransient<TicketProjects>() // 2026-10-13-9a59h
            .AddTransient<TicketStandings>()
            .AddTransient<TicketStuckRule>()
            .AddTransient<TicketCheckStore>()
            .AddTransient<TicketTrackerObserver>()
            .AddTransient<TicketBetweenAttemptsChecker>()
            .AddSingleton<TicketCheckKicks>()
            .AddHostedService<TicketCheckKickWorker>()
            .AddTransient<ConversationRunRecords>()
            .AddSingleton<TicketThreadReader>()
            .AddTransient<TicketTranscriptReader>()
            .AddTransient<TicketConversationReader>()
            .AddSingleton<TicketTrackerTextReader>()
            .AddTransient<TicketTextSnapshotReader>()
            .AddTransient<TicketTextReader>()
            .AddMemoryCache();
        return services;
    }
}
