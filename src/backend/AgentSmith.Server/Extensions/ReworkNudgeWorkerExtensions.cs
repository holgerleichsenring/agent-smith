using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-08-0781: the rework worker and what it reads and says with — singletons on every
/// replica; each nudge is claimed by one of them and handled under the ticket's lock.
/// </summary>
internal static class ReworkNudgeWorkerExtensions
{
    internal static IServiceCollection AddReworkNudgeWorker(this IServiceCollection services)
    {
        services.AddSingleton<IReworkPendingActs, ReworkPendingActs>();
        services.AddSingleton<ReworkSpeech>();
        services.AddSingleton<ReworkWithheld>();
        services.AddSingleton<ReworkFallbackSpawn>();
        services.AddSingleton<ReworkNudgeHandler>().AddSingleton<ClosedTicketActFollowUp>(); // 2026-10-13-9a59t
        services.AddSingleton<PrReviewEntryBuilder>().AddSingleton<PrRecordCiRead>().AddSingleton<PrRecordTargetRead>().AddSingleton<PrReviewRecordRefresh>(); // 2026-10-13-9a59b, 3435a
        services.AddSingleton<IPrRecordRefreshNow, PrRecordRefreshNow>(); // 2026-11-24-a4e7: an act's refresh at once
        services.AddSingleton<IMergeGreenRun, MergeGreenRun>(); // 2026-11-17-3435b; 2026-11-25-96aea: the series master's merge only
        services.AddSingleton<TicketLandingRecorder>().AddSingleton<StackedMergeLanding>(); // 2026-10-14-dbceg, 2026-11-22-6d85b
        // 2026-11-17-023fb: the scoped launch refresh runs start with; 2026-11-25-96aea: a conflict is the series master's to refresh.
        services.TryAddScoped<Infrastructure.Persistence.Repositories.InitRunRepository>();
        services.AddScoped<Services.Branches.BaseRefreshLauncher>();
        services.AddSingleton<Services.Branches.RefreshableTicket>();
        services.AddSingleton<Services.Branches.BaseRefreshStart>().AddSingleton<Services.Branches.BaseRefreshRetry>(); // 2026-11-24-9dc0e
        services.AddSingleton<Services.Branches.BaseRefreshAsked>(); // 2026-11-25-9992e: the series master's refresh
        services.AddSingleton<DependentsWake>().AddSingleton<WaitingTicketStart>(); // 2026-10-14-dbceh; 1b72a
        services.AddSingleton<StackedDependentsWake>(); // 9a39a
        services.AddSingleton<TicketStartHold>().AddSingleton<PreconditionRelease>(); // 2026-11-16-f3fbc
        services.AddSingleton<ReworkNoActTail>().AddSingleton<ISeriesAutoStart, SeriesAutoStart>(); // 2026-10-13-8762v
        services.AddSeriesWakes(); // 2026-11-25-9992g
        services.AddSingleton<ReworkNudgeWorker>();
        return services.AddWorker<ReworkNudgeWorker>();
    }
}
