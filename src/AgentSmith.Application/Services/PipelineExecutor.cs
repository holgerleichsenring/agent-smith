using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Executes a pipeline by building contexts from command names and dispatching
/// them through the CommandExecutor sequentially. Stops on first failure.
/// </summary>
public sealed class PipelineExecutor(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ILogger<PipelineExecutor> logger) : IPipelineExecutor
{
    public async Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Starting pipeline with {Count} commands", commandNames.Count);

        for (var i = 0; i < commandNames.Count; i++)
        {
            var commandName = commandNames[i];
            logger.LogInformation(
                "[{Step}/{Total}] Executing {Command}...",
                i + 1, commandNames.Count, commandName);

            var commandContext = contextFactory.Create(
                commandName, projectConfig, context);

            var result = await ExecuteCommandAsync(commandContext, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Pipeline stopped at step {Step}: {Command} failed - {Message}",
                    i + 1, commandName, result.Message);
                return result;
            }

            logger.LogInformation(
                "[{Step}/{Total}] {Command} completed: {Message}",
                i + 1, commandNames.Count, commandName, result.Message);
        }

        logger.LogInformation("Pipeline completed successfully");
        return CommandResult.Ok("Pipeline completed successfully");
    }

    private Task<CommandResult> ExecuteCommandAsync(
        ICommandContext context, CancellationToken ct)
    {
        return context switch
        {
            FetchTicketContext c => commandExecutor.ExecuteAsync(c, ct),
            CheckoutSourceContext c => commandExecutor.ExecuteAsync(c, ct),
            LoadCodingPrinciplesContext c => commandExecutor.ExecuteAsync(c, ct),
            AnalyzeCodeContext c => commandExecutor.ExecuteAsync(c, ct),
            GeneratePlanContext c => commandExecutor.ExecuteAsync(c, ct),
            ApprovalContext c => commandExecutor.ExecuteAsync(c, ct),
            AgenticExecuteContext c => commandExecutor.ExecuteAsync(c, ct),
            TestContext c => commandExecutor.ExecuteAsync(c, ct),
            CommitAndPRContext c => commandExecutor.ExecuteAsync(c, ct),
            _ => throw new ConfigurationException(
                $"Unknown context type: {context.GetType().Name}")
        };
    }
}
