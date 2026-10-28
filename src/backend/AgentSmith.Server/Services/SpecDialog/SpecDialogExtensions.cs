using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Registers the spec-dialog session flow (p0315a): command parsing, scope
/// resolution, per-thread session management over the relational store, and
/// the routing branch consumed by the message dispatcher. p0315b adds the
/// design-turn machinery: turn runner (in-process pipeline run), turn gate,
/// pending-question registry, and the ask_human question pump.
/// </summary>
internal static class SpecDialogExtensions
{
    internal static IServiceCollection AddSpecDialogServices(this IServiceCollection services)
    {
        services.AddTransient<SpecCommandParser>();
        services.AddTransient<SpecDialogReplyComposer>();
        services.AddTransient<SpecDialogScopeResolver>();
        services.AddSingleton<SpecDialogMessenger>();
        // Singletons: process-lifetime, in-memory coordination state (a running
        // turn is an in-process loop, so the guards live and die with it).
        services.AddSingleton<SpecDialogTurnGate>();
        services.AddSingleton<SpecDialogPendingQuestions>();
        services.AddTransient<SpecDialogQuestionPump>();
        // Scoped: the session manager rides the per-message DI scope's unit of
        // work (SpecDialogSessionRepository -> AgentSmithDbContext); the turn
        // runner shares that scope for the duration of its in-process run.
        services.AddScoped<SpecDialogSessionManager>().AddScoped<BoundSessionNudge>(); // 2026-10-13-9a59g
        services.AddScoped<SpecDialogResumer>();
        services.AddScoped<SpecDialogAnswerAdmission>();
        services.AddScoped<SpecDialogCommandHandler>();
        services.AddScoped<ISpecDialogTurnRunner, SpecDialogTurnRunner>();
        // 2026-09-13-ed5a: the declared templates the epic analysis may read, and the
        // provenance it stamps on the outcome for a filer that runs after they are gone.
        services.AddTransient<SpecDialogReadOnlyScopes>();
        // p0315e: outcome resolution — confirmation gate + durable outcome
        // store. p0315c: the sink files real tickets via the active scope's
        // tracker (renderer + filer), replacing the session-store default.
        services.AddTransient<SpecDialogOutcomeComposer>();
        services.AddTransient<SpecDialogOutcomeConfirmer>().AddTransient<SpecDialogUnreviewedApproval>(); // 2026-10-13-8762i
        services.AddSingleton<AgentSmith.Server.Contracts.IOpenDialogQuestions, RedisOpenDialogQuestions>().AddScoped<SpecDialogButtonAnswer>(); // 2026-10-26-0f19b
        services.AddTransient<SpecTicketRenderer>();
        services.AddTransient<BugTicketRenderer>();
        // 2026-09-17-042eg: what makes a filed work ticket actually start, and what says why it did not.
        // 2026-09-20-2ba8: the tag that lets it resolve at all goes on in the same step.
        services.AddScoped<FiledWorkTagger>();
        services.AddScoped<FiledWorkStarter>().AddScoped<FiledWorkMove>(); // 2026-10-13-9a59m: the move, shared
        services.AddScoped<SpecDialogOutcomeStore>();
        services.AddScoped<SpecDialogLatestOutcomeStore>();
        // 2026-09-17-0e79a: filing a spec stores the approved set under the ticket's spec key.
        // 2026-09-22-b3d7: one path does that for a lone spec and for an approved cut alike.
        services.AddScoped<ApprovedSpecSetRecorder>();
        services.AddScoped<ApprovedSetTicketFiler>();
        // 2026-09-22-b6ad: the approved set reaches the ticket branch as the ticket is filed.
        services.AddScoped<FiledSpecBranchWrite>();
        services.AddScoped<OutcomeTicketFiler>().AddScoped<TicketAmendment>().AddScoped<TicketDescriptionRewrite>().AddTicketContinuation().AddTicketGraph(); // 8e51e, 9a59l, dbcek
        services.AddTransient<FiledSeriesFactory>(); // 2026-10-06-03c7c
        services.AddScoped<IOutcomeSink, TicketFilingOutcomeSink>();
        services.AddScoped<SpecDialogOutcomeFlow>().AddTransient<RatifiedMockStamp>();
        // 2026-09-20-4b0af: the subject a conversation is headed with, minted by the router in
        // the same act that persists the first assistant turn. Scoped for the session
        // repository's unit of work, which is where it stores what it minted. The edit re-entry
        // shares that unit of work for the same reason.
        services.AddScoped<SpecDialogSubjectMinter>().AddSpecDialogDigest(); // dbceo: keeper and digest writer
        services.AddScoped<SpecDialogEditReload>();
        services.AddScoped<SpecDialogRouter>();
        // 2026-09-15-9033: the dashboard channel — the ownership guard rides the session
        // manager's own scoped unit of work, and the dispatcher is the one way in.
        services.AddScoped<SpecDialogOwnership>().AddScoped<MockRatification>();
        services.AddScoped<TicketConversationBinder>().AddScoped<BoundTicketReaders>().AddScoped<TicketRunsFacts>()
            .AddScoped<TicketSearchAcrossTrackers>().AddTransient<TicketRunEndingsReader>();  // 8e51b, 481ba, 5c1eb, 9a59o
        services.AddScoped<TicketTextForConversation>().AddScoped<TicketMovedCheck>()
            .AddScoped<ApprovedSetDivergence>();  // 8e51c, 481ba, 8e51e
        services.AddScoped<TicketProjectChoice>().AddScoped<TicketProjectForTracker>();  // 8e51a, 481bb
        services.AddScoped<ApprovedSetForConversation>();  // 8e51d
        services.AddScoped<DashboardDialogDispatcher>();
        // 2026-09-15-cb3e: the dialog page's read — scoped for the session manager's unit of work,
        // the catalog transient because it re-reads the configuration per call.
        services.AddTransient<SpecDialogProjectCatalog>();
        services.AddScoped<SpecDialogViewReader>();
        services.AddScoped<SpecDialogConversationList>();
        // 2026-09-18-7a05: and the delete of one, over the same scoped unit of work that sweeps
        // the session row and its durable answers in one transaction.
        services.AddScoped<ISpecDialogConversationDeleter, SpecDialogConversationDeleter>();
        // 2026-09-20-3af8: the images an operator attaches — bounded before the body is read,
        // read for their kind, stored against the conversation, and seeded into its next turn.
        services.AddSingleton<SpecDialogImageBody>();
        services.AddSingleton<ImageKindFromBytes>();
        services.AddScoped<SpecDialogConversationResolver>();
        services.AddScoped<SpecDialogTurnImages>();
        // 2026-09-17-042ej: the filed-work read and the watch that keeps it live. The registry is
        // a singleton because it holds CONNECTIONS, which outlive the scope that registered them.
        services.AddScoped<FiledWorkFiling>().AddScoped<FiledWorkBoundTicket>(); // 2026-09-25-c4a6
        services.AddTransient<FiledWorkTrackerProjects>();
        services.AddTransient<FiledWorkSpecReviews>();
        services.AddTransient<FiledWorkRunsReader>();
        services.AddTransient<FiledWorkHandbacks>();
        services.AddScoped<FiledWorkReader>();
        services.AddSingleton<FiledWorkWatchRegistry>();
        services.AddScoped<FiledWorkWatch>();
        // 2026-09-22-9519: the way back out of a filing. The NUDGE is optional — the hub it needs
        // is added conditionally, and a server with the UI API off still runs conversations on chat.
        services.AddTransient<IFiledTicketWithdrawal>(sp => new FiledTicketWithdrawal(
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<AgentSmithConfig>(),
            sp.GetRequiredService<ITicketProviderFactory>(), sp.GetRequiredService<FiledWorkTrackerProjects>(),
            sp.GetService<Events.FiledWorkNudge>(), sp.GetRequiredService<ILogger<FiledTicketWithdrawal>>()));
        // 2026-09-15-6d9c: the proposal pane — what a turn would file, and what filing created.
        services.AddTransient<SpecDialogProposalComposer>();
        services.AddSingleton<DashboardOutcomeChannel>();
        // 2026-09-17-c7aec: which repositories a dashboard design turn opens, as it opens them.
        services.AddSingleton<DashboardReadingChannel>().AddScoped<TicketReadReports>().AddScoped<TicketDiscussion>();
        services.AddSingleton<DashboardActivityChannel>();  // 042ee: between reads and answer
        return services;
    }
}
