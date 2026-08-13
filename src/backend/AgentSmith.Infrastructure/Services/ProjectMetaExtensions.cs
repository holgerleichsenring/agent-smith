using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Project-meta resolution under the target SourcePath (p0104) plus the
/// per-pipeline IRunStateConcepts factory (p0125b). Concepts are bound to a
/// PipelineContext (not a DI singleton) — the factory pulls vocabulary from
/// the context's ConceptVocabulary slot, falling back to Empty when no skills
/// are loaded yet (test fixtures).
/// </summary>
public static class ProjectMetaExtensions
{
    public static IServiceCollection AddProjectMeta(this IServiceCollection services)
    {
        services.AddSingleton<IProjectMetaResolver, ProjectMetaResolver>();
        services.AddSingleton<IProjectBriefBuilder, ProjectBriefBuilder>();
        // p0401: the one-builder rule made injectable — one codec config per host.
        services.AddSingleton<ContextYamlBuilders>();
        services.AddSingleton<IContextYamlSerializer, ContextYamlSerializer>();
        services.AddSingleton<IContextYamlParser, ContextYamlParser>();
        // p0375: registry_auth section read/upsert through the same builder config.
        services.AddSingleton<IContextYamlRegistryAuthCodec, ContextYamlRegistryAuthCodec>();
        services.AddSingleton<Func<PipelineContext, IRunStateConcepts>>(_ => CreateRunStateConcepts);
        return services;
    }

    private static IRunStateConcepts CreateRunStateConcepts(PipelineContext context)
    {
        var vocabulary = context.TryGet<ConceptVocabulary>(ContextKeys.ConceptVocabulary, out var loaded)
            && loaded is not null
                ? loaded
                : ConceptVocabulary.Empty;
        return new PipelineContextRunStateConcepts(context, vocabulary);
    }
}
