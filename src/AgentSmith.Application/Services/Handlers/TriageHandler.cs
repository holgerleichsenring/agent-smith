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
/// in context, then delegates. All triage logic lives in the strategies.
/// </summary>
public sealed class TriageHandler(
    ILlmClientFactory llmClientFactory,
    ITriageStrategySelector strategySelector,
    ILogger<TriageHandler> logger) : ICommandHandler<TriageContext>
{
    public async Task<CommandResult> ExecuteAsync(
        TriageContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var pipelineType = pipeline.TryGet<PipelineType>(
            ContextKeys.PipelineTypeName, out var t) ? t : PipelineType.Discussion;
        var strategy = strategySelector.Select(pipelineType);
        logger.LogDebug("Triage strategy selected: {Strategy} (pipeline_type={PipelineType})",
            strategy.GetType().Name, pipelineType);
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await strategy.ExecuteAsync(pipeline, llmClient, cancellationToken);
    }
}
