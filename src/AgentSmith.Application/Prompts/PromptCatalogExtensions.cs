using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Prompts;

/// <summary>
/// Prompt catalog: embedded prompts + env-directory override source. Both are
/// stateless once constructed (the catalog loads embedded resources once at
/// construction, the override source reads from a directory each call); shared
/// across the application lifetime, so Singleton.
/// </summary>
public static class PromptCatalogExtensions
{
    public static IServiceCollection AddPromptCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IPromptOverrideSource, EnvDirectoryPromptOverrideSource>();
        services.AddSingleton<IPromptCatalog, EmbeddedPromptCatalog>();
        return services;
    }
}
