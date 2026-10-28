using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59l: what an approval in a ticket's own conversation needs. The ticket readers it
/// shares with the dashboard API are added only when that API did not add them — a server with the UI
/// API off still runs conversations on chat. The approval preview's read lives here beside the act it previews,
/// and the start a bound turn may ask for beside the approval it reuses.
/// </summary>
internal static class TicketContinuationRegistrations
{
    internal static IServiceCollection AddTicketContinuation(this IServiceCollection services)
    {
        services.TryAddTransient<TicketAttempts>();
        services.TryAddTransient<TicketRunsQuery>();
        services.TryAddTransient<TicketSeriesRecordsReader>();
        services.TryAddTransient<TicketProjects>();
        services.TryAddTransient<Tickets.Conversation.TicketTranscriptReader>();
        services.TryAddScoped<FiledWorkTagger>();
        services.TryAddScoped<FiledWorkMove>();
        // Starting the next attempt from a bound turn: the pending edit as its approval, else a conversation Retry.
        services.AddRunRetry().AddTransient<PendingSeriesEditReader>().AddScoped<PendingEditStart>().AddScoped<BoundAttemptStarts>();
        // Reading and editing the series from a bound turn: the series as the next attempt reads it, an edit as an approval without a start.
        services.AddScoped<NextAttemptSeriesReader>().AddTransient<SeriesEditList>().AddScoped<SeriesEditAct>().AddScoped<BoundSeriesPorts>();
        return services.AddScoped<TicketContinuationStateReader>()
            .AddScoped<TicketApprovalPreview>()
            .AddScoped<TicketContinuationAct>().AddScoped<TicketCommentPost>() // 2026-10-28-7a69c
            .AddScoped<ClosedTicketFollowUp>()
            .AddScoped<FollowUpLink>()
            .AddScoped<OperatorStartArm>().AddScoped<OperatorStart>(); // 2026-10-13-9a59m
    }
}
