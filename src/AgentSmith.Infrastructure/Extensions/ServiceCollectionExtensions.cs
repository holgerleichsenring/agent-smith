using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

/// <summary>
/// Registers all infrastructure services with the DI container. Helpers are split
/// into partial files by subdomain (TicketProviders, SourceProviders, ChatClients,
/// OutputStrategies, SecurityScanners, Dialogue, ConfigLoading) so each file stays
/// under the 120-line limit. The public entry point lives here.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithInfrastructure(this IServiceCollection services)
    {
        services.AddAgentSmithCore();
        // p0137b: AddHttpClient() is called once at the composition root (Server's
        // Program.cs / CLI's ServiceProviderFactory). Per-feature extensions use
        // AddHttpClient<T>() typed clients instead of the bare factory registration.
        AddTicketProviders(services);
        AddSourceProviders(services);
        AddChatClients(services);
        AddOutputStrategies(services);
        AddSecurityScanners(services);
        AddDialogue(services);
        // Project-meta resolution under target SourcePath (p0104)
        services.AddSingleton<IProjectMetaResolver, Services.ProjectMetaResolver>();
        services.AddSingleton<IProjectBriefBuilder, Services.ProjectBriefBuilder>();
        services.AddSingleton<IBaselineLoader, Services.BaselineLoader>();
        // p0125b: PipelineContextRunStateConcepts is bound to a per-pipeline PipelineContext
        // (not a DI singleton). Register a factory so handlers (p0125c) can inject the
        // creation surface; the factory pulls vocabulary from the context's ConceptVocabulary
        // slot, falling back to Empty when no skills are loaded yet (test fixtures).
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
