using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-23-933db: the PoC runs' steps, the preset guard every run passes, and the defaults a composition
/// without a database keeps: no project is a PoC project and no PoC row exists to take in.</summary>
public static class PocHandlersExtensions
{
    public static IServiceCollection AddPocHandlers(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocProjects, NoPocProjects>();
        services.TryAddSingleton<IPocIntakeStore, NoPocIntakeStore>();
        return services.AddSingleton<PocProjectPresetGuard>().AddTransient<PocArchiveLimitsSource>()
            .AddTransient<PocImportedPublisher>()
            .AddTransient<ICommandHandler<PocIntakeContext>, PocIntakeHandler>();
    }
}
