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
        return services.AddAdminPipelines();
    }
}
