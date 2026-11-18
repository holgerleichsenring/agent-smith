using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the steps of the runs on the installation. Called by the server's composition only, which
/// replaces the empty tool surface and the answer-only outcome with its own.
/// </summary>
public static class AdminPipelineExtensions
{
    public static IServiceCollection AddAdminPipelines(this IServiceCollection services)
    {
        services.TryAddSingleton<IAdminToolSurface, NoAdminTools>();
        services.TryAddSingleton<IAdminTurnOutcome, AnswerOnlyAdminOutcome>();
        services.AddTransient<AdminTurnTools>();
        services.AddTransient<ICommandHandler<AdminAssistantTurnContext>, AdminAssistantTurnHandler>();
        // 2026-11-17-f3a8c: the config run.
        services.AddSingleton<ConfigEntityKinds>().AddScoped<ConfigChangeApplier>().AddScoped<ConfigChangedSignal>();
        services.AddTransient<ICommandHandler<ApplyConfigChangesContext>, ApplyConfigChangesHandler>();
        return services;
    }
}
