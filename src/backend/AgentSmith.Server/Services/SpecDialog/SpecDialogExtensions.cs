using AgentSmith.Application.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;

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
        // 2026-09-13-a72a: an epic's children are filed in dependency order, so a child's
        // predecessor stamp can name a ticket that already exists.
        services.AddTransient<EpicChildOrderer>();
        services.AddScoped<EpicTicketFiler>();
        services.AddScoped<SpecDialogOutcomeStore>();
        services.AddScoped<SpecDialogLatestOutcomeStore>();
        services.AddScoped<OutcomeTicketFiler>();
        services.AddScoped<IOutcomeSink, TicketFilingOutcomeSink>();
        services.AddScoped<SpecDialogOutcomeFlow>();
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
        // 2026-09-15-6d9c: the proposal pane's own delivery — what a turn would file, and
        // what filing it actually created.
        services.AddTransient<SpecDialogProposalComposer>();
        services.AddSingleton<DashboardOutcomeChannel>();
        return services;
    }
}
