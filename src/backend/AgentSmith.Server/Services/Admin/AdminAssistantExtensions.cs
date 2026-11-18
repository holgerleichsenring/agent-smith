using AgentSmith.Application.Services.Admin;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the conversation turn runners — the design partner's and the admin assistant's, chosen per
/// conversation — and the admin assistant's pipelines, tools and outcome.
/// </summary>
public static class AdminAssistantExtensions
{
    public static IServiceCollection AddConversationTurnRunners(this IServiceCollection services)
    {
        services.AddScoped<SpecDialogTurnRunner>().AddScoped<AdminTurnRunner>();
        services.AddScoped<ISpecDialogTurnRunner, ConversationTurnRunner>();
        // 2026-11-17-f3a8b: the server's read tools, scoped with the turn's unit of work.
        services.AddScoped<Read.AdminReadSettings>().AddScoped<Read.AdminRunTools>().AddScoped<Read.AdminCallTools>()
            .AddScoped<Read.AdminEventTools>().AddScoped<Read.AdminPromptTools>()
            .AddScoped<Read.AdminConfigReadTools>().AddScoped<Read.AdminHealthTools>();
        services.AddScoped<IAdminToolSurface, AdminToolSurface>();
        // 2026-11-17-f3a8c: a turn proposes what it drafted; Apply is a config run.
        services.AddSingleton<IAdminTurnOutcome, ConfigDraftOutcome>();
        services.AddScoped<Config.IConfigApplyRun, Config.ConfigApplyRun>().AddScoped<Config.ConfigOutcomeSink>();
        services.AddScoped<DialogDeciderGrants>(); // 2026-11-18-78efa: set by the dashboard dispatch of the decider's message
        // 2026-11-17-f3a8d: the secret values, written by a secret run and read without their values.
        services.AddScoped<Secrets.ISecretWriteRun, Secrets.SecretWriteRun>().AddScoped<Secrets.SecretValuesReader>();
        return services.AddAdminPipelines();
    }
}
