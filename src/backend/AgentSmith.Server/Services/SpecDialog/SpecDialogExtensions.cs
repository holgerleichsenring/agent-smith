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
        services.AddScoped<SpecDialogCommandHandler>();
        services.AddScoped<ISpecDialogTurnRunner, SpecDialogTurnRunner>();
        // p0315e: outcome resolution — confirmation gate, durable outcome
        // store, and the filing seam (SessionStoreOutcomeSink is the honest
        // default until p0315c replaces it with ITicketProvider filing).
        services.AddTransient<SpecDialogOutcomeComposer>();
        services.AddTransient<SpecDialogOutcomeConfirmer>();
        services.AddScoped<SpecDialogOutcomeStore>();
        services.AddScoped<IOutcomeSink, SessionStoreOutcomeSink>();
        services.AddScoped<SpecDialogOutcomeFlow>();
        services.AddScoped<SpecDialogRouter>();
        return services;
    }
}
