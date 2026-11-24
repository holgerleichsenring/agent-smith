using AgentSmith.Application.Services.Pocs.Rollout;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Application.Services.Pocs.Build;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Application.Services.Pocs.Scan;
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
        services.TryAddSingleton<IPocMaskValues, NoPocMaskValues>(); // 2026-11-23-933dg
        services.TryAddSingleton<IPocVariableValues, NoPocVariableValues>();
        services.TryAddSingleton<IPocVariableStore, Pocs.Variables.NoPocVariableStore>();
        services.TryAddSingleton<IPocSealFingerprint, Pocs.Sealing.NoPocSealFingerprint>();
        services.AddSingleton<PocRunMasking>().AddSingleton<Pocs.Variables.PocVariableRules>()
            .AddTransient<Pocs.Sealing.PocSealPlanner>().AddTransient<Pocs.Sealing.PocSealer>()
            .AddTransient<ICommandHandler<SealPocVariablesContext>, SealPocVariablesHandler>();
        return services.AddSingleton<PocProjectPresetGuard>().AddTransient<PocArchiveLimitsSource>()
            .AddTransient<PocImportedPublisher>()
            .AddTransient<ICommandHandler<PocIntakeContext>, PocIntakeHandler>()
            .AddPocScan().AddPocBuild().AddPocGitOps().AddPocRollout().AddPocActs().AddPocLifetime(); // 2026-11-23-933dc, 933dd, c81eb, c81ec, c81ed, c81ee
    }
}
