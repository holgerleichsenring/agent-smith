using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// The skills catalog a binary resolves against — where it comes from, how it is cached
/// and unpacked, and what the resolved catalog carries. Extracted from
/// <c>AddAgentSmithCore</c> unchanged and in the same order (the four
/// <see cref="ISkillsSourceHandler"/> registrations are enumerated in registration order).
/// </summary>
public static class SkillsRegistrations
{
    public static IServiceCollection AddSkillsCatalog(this IServiceCollection services)
    {
        services.AddHttpClient<ISkillsRepositoryClient, SkillsRepositoryClient>();
        services.AddSingleton<ISkillsCacheMarker, SkillsCacheMarker>();
        services.AddTransient<ICatalogTarballExtractor, CatalogTarballExtractor>();
        services.AddSingleton<IEmbeddedSkillsCatalog, EmbeddedSkillsCatalog>();
        services.AddSingleton<SkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath>(sp => sp.GetRequiredService<SkillsCatalogPath>());
        // p0504: the domain profiles the resolved catalog carries.
        services.AddSingleton<IDomainProfileCatalog, FileDomainProfileCatalog>();
        // p0379: authored principles core+delta composition from the resolved catalog.
        services.AddSingleton<IPrinciplesTemplateSource, CatalogPrinciplesTemplateSource>();
        services.AddSingleton<ISkillsSourceHandler, DefaultSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, PathSourceHandler>();
        services.AddSingleton<ISkillsSourceHandler, UrlSourceHandler>();
        // p0325: the embedded catalog is the default resolution when no
        // explicit skills source is configured.
        services.AddSingleton<ISkillsSourceHandler, EmbeddedSourceHandler>();
        services.AddSingleton<ISkillsOverlayMaterializer, SkillsOverlayMaterializer>(); // p0514
        services.AddSingleton<ISkillsCatalogResolver, SkillsCatalogResolver>();
        // p0358: eager, logged catalog refresh when a config reload changes skills.version.
        services.AddSingleton<SkillsCatalogRefresher>();
        return services;
    }
}
