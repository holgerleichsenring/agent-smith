using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;


namespace AgentSmith.Application.Services;

/// <summary>
/// Executes a pipeline by building contexts from command names and dispatching
/// them through the CommandExecutor sequentially. Stops on first failure.
/// Posts status updates and error reports to the ticket provider.
/// </summary>
public sealed class PipelineExecutor(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
    IProgressReporter progressReporter,
    ILogger<PipelineExecutor> logger) : IPipelineExecutor
{
    private static readonly Dictionary<string, string> StepLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FetchTicketCommand"] = "Fetching ticket",
        ["CheckoutSourceCommand"] = "Checking out source",
        ["LoadCodingPrinciplesCommand"] = "Loading coding principles",
        ["AnalyzeCodeCommand"] = "Analyzing codebase",
        ["GeneratePlanCommand"] = "Generating plan",
        ["ApprovalCommand"] = "Awaiting approval",
        ["AgenticExecuteCommand"] = "Executing plan",
        ["GenerateTestsCommand"] = "Generating tests",
        ["TestCommand"] = "Running tests",
        ["GenerateDocsCommand"] = "Generating docs",
        ["CommitAndPRCommand"] = "Creating pull request",
    };

    public async Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Starting pipeline with {Count} commands", commandNames.Count);

        await PostTicketStatusAsync(projectConfig, context,
            "Agent Smith is working on this issue...", cancellationToken);

        for (var i = 0; i < commandNames.Count; i++)
        {
            var commandName = commandNames[i];
            var step = i + 1;
            var total = commandNames.Count;
            var label = StepLabels.GetValueOrDefault(commandName, commandName);

            logger.LogInformation(
                "[{Step}/{Total}] Executing {Command}...",
                step, total, commandName);

            await progressReporter.ReportProgressAsync(step, total, label, cancellationToken);

            var commandContext = contextFactory.Create(
                commandName, projectConfig, context);

            var result = await ExecuteCommandAsync(commandContext, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Pipeline stopped at step {Step}: {Command} failed - {Message}",
                    i + 1, commandName, result.Message);

                await PostTicketStatusAsync(projectConfig, context,
                    $"## Agent Smith - Failed\n\n**Step:** {commandName} ({i + 1}/{commandNames.Count})\n**Error:** {result.Message}",
                    cancellationToken);

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

    private async Task PostTicketStatusAsync(
        ProjectConfig projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
    {
        try
        {
            if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
                return;

            var ticketProvider = ticketFactory.Create(projectConfig.Tickets);
            await ticketProvider.UpdateStatusAsync(ticketId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post status update to ticket");
        }
    }
}
