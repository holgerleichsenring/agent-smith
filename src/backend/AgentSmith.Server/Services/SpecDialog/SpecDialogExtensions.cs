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
        services.AddScoped<SpecDialogSessionManager>();
        services.AddScoped<SpecDialogResumer>();
        services.AddScoped<SpecDialogAnswerAdmission>();
        services.AddScoped<SpecDialogCommandHandler>();
        services.AddScoped<ISpecDialogTurnRunner, SpecDialogTurnRunner>();
        // 2026-09-13-ed5a: the declared templates the epic analysis may read, and the
        // provenance it stamps on the outcome for a filer that runs after they are gone.
        services.AddTransient<SpecDialogTemplateScopes>();
        // p0315e: outcome resolution — confirmation gate + durable outcome
        // store. p0315c: the sink files real tickets via the active scope's
        // tracker (renderer + filer), replacing the session-store default.
        services.AddTransient<SpecDialogOutcomeComposer>();
        services.AddTransient<SpecDialogOutcomeConfirmer>();
        services.AddTransient<PhaseTicketRenderer>();
        services.AddTransient<BugTicketRenderer>();
        // 2026-09-17-0e79d: the order is the approved SET's — one run works the slices in it.
        services.AddTransient<EpicChildOrderer>();
        // 2026-09-17-042eg: what makes a filed work ticket actually start, and what says why it did not.
        // 2026-09-20-2ba8: the tag that lets it resolve at all goes on in the same step.
        services.AddScoped<FiledWorkTagger>();
        services.AddScoped<FiledWorkStarter>();
        services.AddScoped<SpecDialogOutcomeStore>();
        services.AddScoped<SpecDialogLatestOutcomeStore>();
        // 2026-09-17-0e79a: filing a phase stores the approved set under the ticket's spec key.
        // 2026-09-22-b3d7: one path does that for a lone phase and for an approved cut alike.
        services.AddScoped<ApprovedPhaseSetRecorder>();
        services.AddScoped<ApprovedSetTicketFiler>();
        // 2026-09-22-b6ad: the approved set reaches the ticket branch as the ticket is filed.
        services.AddScoped<FiledSpecBranchWrite>();
        services.AddScoped<OutcomeTicketFiler>();
        services.AddScoped<IOutcomeSink, TicketFilingOutcomeSink>();
        services.AddScoped<SpecDialogOutcomeFlow>();
        // 2026-09-20-4b0af: the subject a conversation is headed with, minted by the router in
        // the same act that persists the first assistant turn. Scoped for the session
        // repository's unit of work, which is where it stores what it minted. The edit re-entry
        // shares that unit of work for the same reason.
        services.AddScoped<SpecDialogSubjectMinter>();
        services.AddScoped<SpecDialogEditReload>();
        services.AddScoped<SpecDialogRouter>();
        // 2026-09-15-9033: the dashboard channel. The ownership guard rides the same
        // scoped unit of work as the session manager it reads through; the dispatcher is
        // the ingestion endpoint's one entry point into the router.
        services.AddScoped<SpecDialogOwnership>();
        services.AddScoped<DashboardDialogDispatcher>();
        // 2026-09-15-cb3e: the dialog page's read. Scoped for the session manager's unit of
        // work; the catalog is transient because it re-reads the configuration per call.
        services.AddTransient<SpecDialogProjectCatalog>();
        services.AddScoped<SpecDialogViewReader>();
        services.AddScoped<SpecDialogConversationList>();
        // 2026-09-18-7a05: and the delete of one, over the same scoped unit of work — the
        // transaction the session row and its durable answers are swept in is that unit's.
        services.AddScoped<ISpecDialogConversationDeleter, SpecDialogConversationDeleter>();
        // 2026-09-20-3af8: the images an operator attaches — bounded before the body is read,
        // read for their kind, stored against the conversation, and seeded into its next turn.
        services.AddSingleton<SpecDialogImageBody>();
        services.AddSingleton<ImageKindFromBytes>();
        services.AddScoped<SpecDialogConversationResolver>();
        services.AddScoped<SpecDialogTurnImages>();
        // 2026-09-17-042ej: the filed-work read and the watch that keeps it live. The registry is
        // a singleton because it holds CONNECTIONS, which outlive the scope that registered them.
        services.AddScoped<FiledWorkFiling>();
        services.AddTransient<FiledWorkTrackerProjects>();
        services.AddTransient<FiledWorkPhaseReviews>();
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
        // 2026-09-15-6d9c: the proposal pane's own delivery — what a turn would file, and
        // what filing it actually created.
        services.AddTransient<SpecDialogProposalComposer>();
        services.AddSingleton<DashboardOutcomeChannel>();
        // 2026-09-17-c7aec: which repositories a dashboard design turn opens, as it opens them.
        services.AddSingleton<DashboardReadingChannel>();
        // 2026-09-17-042ee: what that turn is doing between the reads and the answer.
        services.AddSingleton<DashboardActivityChannel>();
        return services;
    }
}
