using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Structure;

/// <summary>2026-10-06-8565b: reading a principles file's Limits and judging a measurement against them.</summary>
public static class StructureJudgingExtensions
{
    public static IServiceCollection AddStructureJudging(this IServiceCollection services)
    {
        services.AddSingleton<StructureLimits>();
        services.AddSingleton<StructureJudge>();
        return services;
    }
}
