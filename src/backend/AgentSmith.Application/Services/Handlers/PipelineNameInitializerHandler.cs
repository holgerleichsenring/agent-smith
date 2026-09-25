using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Publishes the <c>pipeline_name</c> concept once at the head of every pipeline
/// chain. Reads <see cref="ResolvedPipelineConfig"/> from <see cref="PipelineContext"/>
/// (set by PipelineConfigResolver) and calls <see cref="IRunStateConcepts.SetEnum"/>;
/// SetEnum throws if the resolved name is not in the declared enum, fencing routing
/// changes before any downstream handler runs.
/// </summary>
public sealed class PipelineNameInitializerHandler(
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<PipelineNameInitializerHandler> logger)
    : ICommandHandler<PipelineNameInitializerContext>, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        [new ConceptDeclaration("pipeline_name", ConceptType.Enum)];

    public Task<CommandResult> ExecuteAsync(
        PipelineNameInitializerContext context, CancellationToken cancellationToken)
    {
        var resolved = context.Pipeline.Get<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline);
        var concepts = conceptsFactory(context.Pipeline);
        // 2026-09-25-e5b1: the resolved name IS the published name. p0393 had aliases to
        // canonicalise here; with the alias map gone a run can only have been started under a
        // name the presets declare, and SetEnum still rejects anything the catalog vocabulary
        // does not — a typo fails loudly rather than mis-keying every downstream rule.
        var published = resolved.PipelineName;
        concepts.SetEnum("pipeline_name", published);
        logger.LogDebug("Published pipeline_name={Name}", published);
        return Task.FromResult(CommandResult.Ok($"pipeline_name={published}"));
    }
}
