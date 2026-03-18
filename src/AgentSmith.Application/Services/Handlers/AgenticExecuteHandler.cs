using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Executes the plan via AI agent agentic loop (tool calling).
/// Writes execution decisions to the decision log after completion.
/// </summary>
public sealed class AgenticExecuteHandler(
    IAgentProviderFactory factory,
    IDecisionLogger decisionLogger,
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

        if (result.Decisions is { Count: > 0 })
        {
            foreach (var d in result.Decisions)
            {
                if (Enum.TryParse<DecisionCategory>(d.Category, true, out var cat))
                    await decisionLogger.LogAsync(
                        context.Repository.LocalPath, cat, d.Decision, cancellationToken);
            }

            context.Pipeline.AppendDecisions(result.Decisions);
        }

        logger.LogInformation(
            "Agentic execution completed: {Count} files changed, {Decisions} decisions",
            result.Changes.Count, result.Decisions?.Count ?? 0);

        return CommandResult.Ok($"Agentic execution completed: {result.Changes.Count} files changed");
    }
}
