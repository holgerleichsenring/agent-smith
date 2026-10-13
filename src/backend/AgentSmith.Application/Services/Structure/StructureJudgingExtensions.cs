using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Structure;

/// <summary>2026-10-06-8565b: reading a principles file's Limits and judging a measurement against them.</summary>
public static class StructureJudgingExtensions
{
    public static IServiceCollection AddStructureJudging(this IServiceCollection services)
    {
        services.AddSingleton<StructureLimits>();
        services.AddSingleton<StructureJudge>();
        services.AddTransient<StructureSourceListing>(); // 2026-10-06-8565c
        services.AddTransient<StructureBaseline>();
        services.AddTransient<StructureGitRunner>(); // 2026-10-13-2d0c
        services.AddTransient<StructureZeroListing>();
        services.AddTransient<StructureBatchedReader>(); // 2026-10-13-ee83
        services.AddTransient<StructureBatchScript>();
        services.AddTransient<StructureBatchOutput>();
        services.AddTransient<StructureBaselineHeader>();
        services.AddTransient<StructureBaselineFile>();
        services.AddTransient<StructureRepoContexts>();
        services.AddTransient<StructureMergeBase>();
        services.AddTransient<StructureGitReads>();
        services.AddTransient<StructureGateContexts>();
        services.AddTransient<StructureGateFiles>();
        services.AddTransient<StructureRepoSandboxes>(); // 2026-10-13-e237
        services.AddTransient<StructureGate>();
        services.AddTransient<StructureGateOutcome>();
        services.AddTransient<StructureDivergencePublisher>(); // 2026-10-12-fbbda
        services.AddTransient<StructureOwnership>(); // 2026-10-13-50c7
        services.AddTransient<StructureBaselineOwnedLine>();
        services.AddTransient<StructureBaselineOwnership>();
        services.AddTransient<StructureBaselineEntries>();
        services.AddTransient<StructureContextSources>();
        services.AddTransient<StructureBaselineScopes>();
        return services;
    }
}
