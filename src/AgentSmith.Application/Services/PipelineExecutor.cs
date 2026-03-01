using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;


namespace AgentSmith.Application.Services;

/// <summary>
/// Executes a pipeline by building contexts from command names and dispatching
/// them through the CommandExecutor sequentially. Stops on first failure.
/// Supports runtime command insertion via CommandResult.InsertNext.
/// Posts status updates and error reports to the ticket provider.
/// </summary>
public sealed class PipelineExecutor(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
    IProgressReporter progressReporter,
    ILogger<PipelineExecutor> logger) : IPipelineExecutor
{
    private const int MaxCommandExecutions = 100;

    public async Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting pipeline with {Count} commands", commandNames.Count);

        await PostTicketStatusAsync(projectConfig, context,
            "Agent Smith is working on this issue...", cancellationToken);

        var commands = new LinkedList<string>(commandNames);
        var current = commands.First;
        var executionCount = 0;

        while (current is not null)
        {
            if (++executionCount > MaxCommandExecutions)
            {
                return CommandResult.Fail(
                    $"Pipeline exceeded maximum of {MaxCommandExecutions} command executions. " +
                    "Possible infinite loop in command insertion.");
            }

            var commandName = current.Value;
            var total = commands.Count;
            var label = CommandNames.GetLabel(commandName);

            logger.LogInformation(
                "[{Step}/{Total}] Executing {Command}...",
                executionCount, total, commandName);

            await progressReporter.ReportProgressAsync(
                executionCount, total, label, cancellationToken);

            CommandResult result;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var commandContext = contextFactory.Create(
                    commandName, projectConfig, context);

                result = await ExecuteCommandAsync(commandContext, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Command {Command} threw an unhandled exception", commandName);
                result = CommandResult.Fail($"{commandName} failed: {ex.Message}");
            }
            finally
            {
                sw.Stop();
            }

            context.TrackCommand(commandName, result.IsSuccess, result.Message,
                sw.Elapsed, result.InsertNext?.Count);

            if (!result.IsSuccess)
            {
                logger.LogWarning(
                    "Pipeline stopped at step {Step}: {Command} failed - {Message}",
                    executionCount, commandName, result.Message);

                await PostTicketStatusAsync(projectConfig, context,
                    $"## Agent Smith - Failed\n\n**Step:** {label} ({executionCount}/{total})\n**Error:** {result.Message}",
                    cancellationToken);

                return result with
                {
                    FailedStep = executionCount,
                    TotalSteps = total,
                    StepName = label
                };
            }

            if (result.InsertNext is { Count: > 0 })
            {
                var insertAfter = current;
                foreach (var next in result.InsertNext)
                {
                    commands.AddAfter(insertAfter, next);
                    insertAfter = insertAfter.Next!;
                }

                logger.LogInformation(
                    "{Command} inserted {Count} follow-up commands: {Commands}",
                    commandName,
                    result.InsertNext.Count,
                    string.Join(", ", result.InsertNext));
            }

            // Post skill-related detail to Slack/progress reporter
            await PostSkillDetailAsync(commandName, result, cancellationToken);

            logger.LogInformation(
                "[{Step}/{Total}] {Command} completed: {Message}",
                executionCount, commands.Count, commandName, result.Message);

            current = current.Next;
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
            LoadDomainRulesContext c => commandExecutor.ExecuteAsync(c, ct),
            AnalyzeCodeContext c => commandExecutor.ExecuteAsync(c, ct),
            GeneratePlanContext c => commandExecutor.ExecuteAsync(c, ct),
            ApprovalContext c => commandExecutor.ExecuteAsync(c, ct),
            AgenticExecuteContext c => commandExecutor.ExecuteAsync(c, ct),
            TestContext c => commandExecutor.ExecuteAsync(c, ct),
            CommitAndPRContext c => commandExecutor.ExecuteAsync(c, ct),
            BootstrapProjectContext c => commandExecutor.ExecuteAsync(c, ct),
            LoadCodeMapContext c => commandExecutor.ExecuteAsync(c, ct),
            LoadContextContext c => commandExecutor.ExecuteAsync(c, ct),
            WriteRunResultContext c => commandExecutor.ExecuteAsync(c, ct),
            InitCommitContext c => commandExecutor.ExecuteAsync(c, ct),
            TriageContext c => commandExecutor.ExecuteAsync(c, ct),
            SwitchSkillContext c => commandExecutor.ExecuteAsync(c, ct),
            SkillRoundContext c => commandExecutor.ExecuteAsync(c, ct),
            ConvergenceCheckContext c => commandExecutor.ExecuteAsync(c, ct),
            GenerateTestsContext c => commandExecutor.ExecuteAsync(c, ct),
            GenerateDocsContext c => commandExecutor.ExecuteAsync(c, ct),
            _ => throw new ConfigurationException(
                $"Unknown context type: {context.GetType().Name}")
        };
    }

    private async Task PostSkillDetailAsync(
        string commandName, CommandResult result, CancellationToken cancellationToken)
    {
        try
        {
            var baseCommand = commandName.Contains(':')
                ? commandName[..commandName.IndexOf(':')]
                : commandName;

            var detail = baseCommand switch
            {
                CommandNames.Triage => $"Triage: {result.Message}",
                CommandNames.SkillRound => $"Skill Round: {result.Message}",
                CommandNames.ConvergenceCheck => $"Convergence: {result.Message}",
                CommandNames.SwitchSkill => $"Skill Switch: {result.Message}",
                _ => null
            };

            if (detail is not null)
                await progressReporter.ReportDetailAsync(detail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to post skill detail");
        }
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
