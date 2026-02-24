using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Executes the plan via AI agent agentic loop (tool calling).
/// </summary>
public sealed class AgenticExecuteHandler(
    IAgentProviderFactory factory,
    IProgressReporter progressReporter,
    ILogger<AgenticExecuteHandler> logger)
    : ICommandHandler<AgenticExecuteContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AgenticExecuteContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing plan with {Steps} steps...", context.Plan.Steps.Count);

        var provider = factory.Create(context.AgentConfig);
        var result = await provider.ExecutePlanAsync(
            context.Plan, context.Repository, context.CodingPrinciples,
            context.CodeMap, context.ProjectContext, progressReporter, cancellationToken);

        context.Pipeline.Set(ContextKeys.CodeChanges, result.Changes);

        if (result.CostSummary is not null)
            context.Pipeline.Set(ContextKeys.RunCostSummary, result.CostSummary);

        if (result.DurationSeconds is not null)
            context.Pipeline.Set(ContextKeys.RunDurationSeconds, result.DurationSeconds.Value);

        logger.LogInformation(
            "Agentic execution completed: {Count} files changed", result.Changes.Count);

        return CommandResult.Ok($"Agentic execution completed: {result.Changes.Count} files changed");
    }
}
