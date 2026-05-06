using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Thin adapter for the TriageCommand. Picks the appropriate ITriageStrategy
/// (legacy LLM discussion vs phase-based structured) based on the pipeline_type
/// in context, then delegates. AgentConfig is stashed in pipeline context for
/// strategies that need it.
/// </summary>
public sealed class TriageHandler(
    ITriageStrategySelector strategySelector,
    ILogger<TriageHandler> logger) : ICommandHandler<TriageContext>
{
    public async Task<CommandResult> ExecuteAsync(
        TriageContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        var pipelineType = pipeline.TryGet<PipelineType>(
            ContextKeys.PipelineTypeName, out var t) ? t : PipelineType.Discussion;
        var strategy = strategySelector.Select(pipelineType);
        logger.LogDebug("Triage strategy selected: {Strategy} (pipeline_type={PipelineType})",
            strategy.GetType().Name, pipelineType);
        return await strategy.ExecuteAsync(pipeline, cancellationToken);
    }
}
