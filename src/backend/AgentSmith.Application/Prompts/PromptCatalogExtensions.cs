using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Prompts;

/// <summary>
/// Prompt catalog: SkillCatalogPromptCatalog adapter (p0179a) wraps the
/// EmbeddedPromptCatalog. The adapter routes the migrated master-prompt names
/// to the loaded master-skill bodies; everything else falls back to embedded.
/// </summary>
public static class PromptCatalogExtensions
{
    public static IServiceCollection AddPromptCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IPromptOverrideSource, EnvDirectoryPromptOverrideSource>();
        services.AddSingleton<EmbeddedPromptCatalog>();
        services.AddSingleton<IPromptCatalog>(sp => new SkillCatalogPromptCatalog(
            sp.GetRequiredService<EmbeddedPromptCatalog>(),
            sp.GetRequiredService<ISkillLoader>(),
            sp.GetRequiredService<ISkillsCatalogPath>(),
            sp.GetRequiredService<ISkillBodyResolver>(),
            sp.GetRequiredService<ILogger<SkillCatalogPromptCatalog>>()));
        return services;
    }
}
