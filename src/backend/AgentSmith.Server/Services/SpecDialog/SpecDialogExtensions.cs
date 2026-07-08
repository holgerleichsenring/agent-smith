using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Registers the spec-dialog session flow (p0315a): command parsing, scope
/// resolution, per-thread session management over the relational store, and
/// the routing branch consumed by the message dispatcher.
/// </summary>
internal static class SpecDialogExtensions
{
    internal static IServiceCollection AddSpecDialogServices(this IServiceCollection services)
    {
        services.AddTransient<SpecCommandParser>();
        services.AddTransient<SpecDialogReplyComposer>();
        services.AddTransient<SpecDialogScopeResolver>();
        services.AddSingleton<SpecDialogMessenger>();
        // Scoped: the session manager rides the per-message DI scope's unit of
        // work (SpecDialogSessionRepository -> AgentSmithDbContext).
        services.AddScoped<SpecDialogSessionManager>();
        services.AddScoped<SpecDialogCommandHandler>();
        services.AddScoped<SpecDialogRouter>();
        return services;
    }
}
