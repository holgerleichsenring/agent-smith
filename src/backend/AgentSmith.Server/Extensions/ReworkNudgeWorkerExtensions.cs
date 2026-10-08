using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<ReworkNudgeHandler>();
        services.AddSingleton<ReworkNudgeWorker>();
        return services.AddHostedService(sp => sp.GetRequiredService<ReworkNudgeWorker>());
    }
}
