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
        concepts.SetEnum("pipeline_name", resolved.PipelineName);
        logger.LogDebug("Published pipeline_name={Name}", resolved.PipelineName);
        return Task.FromResult(CommandResult.Ok($"pipeline_name={resolved.PipelineName}"));
    }
}
