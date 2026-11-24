using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: the lifecycle acts' first step, its table and the no-database default of its store.</summary>
public static class PocActsExtensions
{
    public static IServiceCollection AddPocActs(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocActStore, NoPocActStore>();
        return services.AddSingleton<PocActPlan>().AddTransient<PocRollbackImage>()
            .AddTransient<ICommandHandler<PocActContext>, PocActHandler>();
    }
}
