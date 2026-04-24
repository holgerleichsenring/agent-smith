using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;


namespace AgentSmith.Application.Services;

/// <summary>
/// Executes a pipeline by building contexts from command names and dispatching
/// them through the CommandExecutor sequentially. Stops on first failure.
/// Supports runtime command insertion via CommandResult.InsertNext.
/// Posts status updates and error reports to the ticket provider.
/// Wraps execution with lifecycle transitions (Enqueued → InProgress → Done/Failed)
/// and a Redis heartbeat when a TicketId is present.
/// </summary>
public sealed class PipelineExecutor(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitionerFactory transitionerFactory,
    IJobHeartbeatService heartbeat,
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

        for (var i = 0; i < commandNames.Count; i++)
            logger.LogInformation("  [{Index}/{Total}] {Command}",
                i + 1, commandNames.Count, commandNames[i]);

        await PostTicketStatusAsync(projectConfig, context,
            "Agent Smith is working on this issue...", cancellationToken);

        await using var lifecycle = await BeginLifecycleAsync(projectConfig, context, cancellationToken);

        var commands = new LinkedList<PipelineCommand>(
            commandNames.Select(PipelineCommand.Simple));
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

            var cmd = current.Value;
            var commandName = cmd.DisplayName;
            var total = commands.Count;
            var label = CommandNames.GetLabel(cmd.Name);

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
                    cmd, projectConfig, context);

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

            context.TrackCommand(cmd.DisplayName, result.IsSuccess, result.Message,
                sw.Elapsed, result.InsertNext?.Count);

            if (!result.IsSuccess)
            {
                logger.LogWarning(
                    "Pipeline stopped at step {Step}: {Command} failed - {Message}",
                    executionCount, commandName, result.Message);

                await PostTicketStatusAsync(projectConfig, context,
                    $"## Agent Smith - Failed\n\n**Step:** {label} ({executionCount}/{total})\n**Error:** {result.Message}",
                    cancellationToken);

                lifecycle.MarkFailed();
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
            await PostSkillDetailAsync(cmd, result, cancellationToken);

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
        => commandExecutor.ExecuteAsync(context, ct);

    private async Task<LifecycleScope> BeginLifecycleAsync(
        ProjectConfig projectConfig, PipelineContext context, CancellationToken ct)
    {
        if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
            return LifecycleScope.Noop;

        try
        {
            var transitioner = transitionerFactory.Create(projectConfig.Tickets);
            var transition = await transitioner.TransitionAsync(
                ticketId, TicketLifecycleStatus.Enqueued, TicketLifecycleStatus.InProgress, ct);
            if (!transition.IsSuccess)
                logger.LogWarning(
                    "Enqueued → InProgress transition {Outcome}: {Error}",
                    transition.Outcome, transition.Error);

            return new LifecycleScope(transitioner, heartbeat.Start(ticketId), ticketId, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start lifecycle tracking — continuing without it");
            return LifecycleScope.Noop;
        }
    }

    private async Task PostSkillDetailAsync(
        PipelineCommand cmd, CommandResult result, CancellationToken cancellationToken)
    {
        try
        {
            var detail = cmd.Name switch
            {
                CommandNames.Triage or CommandNames.SecurityTriage or CommandNames.ApiSecurityTriage
                    => $"Triage: {result.Message}",
                CommandNames.SkillRound or CommandNames.SecuritySkillRound or CommandNames.ApiSecuritySkillRound
                    => $"Skill Round: {result.Message}",
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

    private sealed class LifecycleScope(
        ITicketStatusTransitioner? transitioner,
        IAsyncDisposable? heartbeat,
        TicketId? ticketId,
        ILogger logger) : IAsyncDisposable
    {
        public static LifecycleScope Noop { get; } = new(null, null, null, NullLogger.Instance);
        private bool _failed;

        public void MarkFailed() => _failed = true;

        public async ValueTask DisposeAsync()
        {
            if (heartbeat is not null) await heartbeat.DisposeAsync();
            if (transitioner is null || ticketId is null) return;

            var target = _failed ? TicketLifecycleStatus.Failed : TicketLifecycleStatus.Done;
            var result = await transitioner.TransitionAsync(
                ticketId, TicketLifecycleStatus.InProgress, target, CancellationToken.None);
            if (!result.IsSuccess)
                logger.LogWarning(
                    "InProgress → {Target} transition {Outcome}: {Error}",
                    target, result.Outcome, result.Error);
        }

        private sealed class NullLogger : ILogger
        {
            public static NullLogger Instance { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel l, EventId e, TState s, Exception? ex, Func<TState, Exception?, string> f) { }
        }
    }
}
