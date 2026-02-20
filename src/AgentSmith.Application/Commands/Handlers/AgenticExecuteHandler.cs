using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

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
        AgenticExecuteContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Executing plan with {Steps} steps...", context.Plan.Steps.Count);

        var provider = factory.Create(context.AgentConfig);
        var changes = await provider.ExecutePlanAsync(
            context.Plan, context.Repository, context.CodingPrinciples,
            progressReporter, cancellationToken);

        context.Pipeline.Set(ContextKeys.CodeChanges, changes);

        logger.LogInformation(
            "Agentic execution completed: {Count} files changed", changes.Count);

        return CommandResult.Ok($"Agentic execution completed: {changes.Count} files changed");
    }
}
