using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: the rollout follow of a PoC run, with the no-database default of its store, and the
/// ApplicationSet part of poc-target-install.</summary>
public static class PocRolloutExtensions
{
    public static IServiceCollection AddPocRollout(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocRolloutStore, NoPocRolloutStore>();
        services.TryAddSingleton(TimeProvider.System);
        return services.AddSingleton<PocRolloutVerdict>().AddSingleton<PocRolloutExpectation>().AddTransient<PocRolloutLines>()
            .AddTransient<PocRolloutDeadline>().AddTransient<PocRolloutWatch>().AddTransient<PocRolloutRecord>()
            .AddTransient<ICommandHandler<PocRolloutFollowContext>, PocRolloutFollowHandler>()
            .AddSingleton<Argo.PocApplicationSet>().AddSingleton<Argo.PocApplicationSetMatch>().AddTransient<Argo.PocApplicationSetInstall>();
    }
}
