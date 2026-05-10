using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Thin adapter for the TriageCommand. Picks the appropriate ITriageStrategy
/// (legacy LLM discussion vs phase-based structured) based on the pipeline_type
/// in context, then delegates. AgentConfig is stashed in pipeline context for
/// strategies that need it. p0125c: logs a concept-snapshot line so a human
/// reading the run log can reproduce any future activates_when expression by hand.
/// </summary>
public sealed class TriageHandler(
    ITriageStrategySelector strategySelector,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<TriageHandler> logger) : ICommandHandler<TriageContext>
{
    public async Task<CommandResult> ExecuteAsync(
        TriageContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);

        LogConceptSnapshot(pipeline);

        var pipelineType = pipeline.TryGet<PipelineType>(
            ContextKeys.PipelineTypeName, out var t) ? t : PipelineType.Discussion;
        var pipelineName = pipeline.TryGet<ResolvedPipelineConfig>(
            ContextKeys.ResolvedPipeline, out var resolved) && resolved is not null
            ? resolved.PipelineName
            : string.Empty;
        var strategy = strategySelector.Select(pipelineType, pipelineName);
        logger.LogDebug("Triage strategy selected: {Strategy} (pipeline_type={PipelineType}, pipeline_name={Name})",
            strategy.GetType().Name, pipelineType, pipelineName);
        return await strategy.ExecuteAsync(pipeline, cancellationToken);
    }

    private void LogConceptSnapshot(PipelineContext pipeline)
    {
        var vocabulary = ResolveVocabulary(pipeline);
        var snapshot = RenderSnapshot(conceptsFactory(pipeline), vocabulary);
        logger.LogInformation("Triage concept snapshot: {Concepts}", snapshot);
    }

    private static ConceptVocabulary ResolveVocabulary(PipelineContext pipeline) =>
        pipeline.TryGet<ConceptVocabulary>(ContextKeys.ConceptVocabulary, out var v) && v is not null
            ? v
            : ConceptVocabulary.Empty;

    private static string RenderSnapshot(IRunStateConcepts state, ConceptVocabulary vocabulary)
    {
        var sb = new StringBuilder();
        foreach (var (name, concept) in vocabulary.Concepts)
        {
            var rendered = TryRenderNonDefault(state, name, concept);
            if (rendered is null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(rendered);
        }
        return sb.Length == 0 ? "(all defaults)" : sb.ToString();
    }

    private static string? TryRenderNonDefault(IRunStateConcepts state, string name, ProjectConcept concept) =>
        concept.Type switch
        {
            ConceptType.Bool => state.GetBool(name) ? $"{name}=true" : null,
            ConceptType.Int => state.GetInt(name) is var i && i != 0 ? $"{name}={i}" : null,
            ConceptType.Enum => state.GetEnum(name) is var e && !string.Equals(e, concept.EnumValues![0], StringComparison.Ordinal)
                ? $"{name}={e}"
                : null,
            _ => null
        };
}
