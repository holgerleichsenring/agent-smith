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
        services.AddSingleton<PrReviewEntryBuilder>().AddSingleton<PrRecordCiRead>().AddSingleton<PrReviewRecordRefresh>(); // 2026-10-13-9a59b, 3435a
        services.AddSingleton<TicketLandingRecorder>(); // 2026-10-14-dbceg
        // 2026-11-17-023fb: the landing hook's first subscriber, and the scoped launch it starts runs with.
        services.AddSingleton<ITicketLandedSubscriber, Services.Branches.SiblingBaseRefresh>();
        services.TryAddScoped<Infrastructure.Persistence.Repositories.InitRunRepository>();
        services.AddScoped<Services.Branches.BaseRefreshLauncher>();
        services.AddSingleton<DependentsWake>().AddSingleton<RequirementsMetStart>(); // 2026-10-14-dbceh
        services.AddSingleton<StackedDependentsWake>().AddSingleton<StackedPullRequestRetarget>().AddSingleton<StackedTicketUpkeep>(); // 9a39a/c
        services.AddSingleton<TicketStartHold>().AddSingleton<PreconditionRelease>(); // 2026-11-16-f3fbc
        services.AddSingleton<ReworkNoActTail>().AddSingleton<ISeriesAutoStart, SeriesAutoStart>(); // 2026-10-13-8762v
        services.AddSingleton<ReworkNudgeWorker>();
        return services.AddHostedService(sp => sp.GetRequiredService<ReworkNudgeWorker>());
    }
}
