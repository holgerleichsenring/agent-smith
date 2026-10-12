using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>2026-10-06-8565b: the structure engine — one catalog, one grammar cache, per process.</summary>
public static class StructureExtensions
{
    public static IServiceCollection AddStructureEngine(this IServiceCollection services)
    {
        services.AddSingleton<StructureLanguageCatalog>();
        services.AddSingleton<StructureGrammarLoader>();
        services.AddSingleton<StructureUnitCollector>();
        services.AddSingleton<IStructureEngine, TreeSitterStructureEngine>();
        return services;
    }
}
