using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;


namespace AgentSmith.Application.Services;

/// <summary>
/// Executes a pipeline by building contexts from command names and dispatching
/// them through the CommandExecutor. Same-(Name, Round) skill-round commands are
/// batched and run in parallel via PipelineBatchRunner when the parallelism knob > 1;
/// otherwise the loop is sequential and behaves exactly as before.
/// Stops on first failure. Supports runtime command insertion via CommandResult.InsertNext.
/// Posts status updates and error reports to the ticket provider.
/// Cross-process lifecycle (status transitions + Redis heartbeat) is delegated to
/// IPipelineLifecycleCoordinator — Server uses a ticket-aware coordinator, CLI uses NoOp.
/// </summary>
public sealed class PipelineExecutor(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
    IPipelineLifecycleCoordinator lifecycleCoordinator,
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder sandboxSpecBuilder,
    IProgressReporter progressReporter,
    ILogger<PipelineExecutor> logger) : IPipelineExecutor
{
    private const int MaxCommandExecutions = 100;

    private static readonly HashSet<string> SandboxRequiringCommands = new(StringComparer.Ordinal)
    {
        CommandNames.CheckoutSource, CommandNames.AgenticExecute, CommandNames.Test,
        CommandNames.GenerateTests, CommandNames.GenerateDocs
    };

    public async Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting pipeline with {Count} commands", commandNames.Count);
        for (var i = 0; i < commandNames.Count; i++)
            logger.LogInformation("  [{Index}/{Total}] {Command}",
                i + 1, commandNames.Count, commandNames[i]);

        await PostTicketStatusAsync(projectConfig, context,
            "Agent Smith is working on this issue...", cancellationToken);

        await using var lifecycle = await lifecycleCoordinator.BeginAsync(projectConfig, context, cancellationToken);
        await using var sandbox = await TryCreateSandboxAsync(commandNames, projectConfig, context, cancellationToken);

        var commands = new LinkedList<PipelineCommand>(
            commandNames.Select(PipelineCommand.Simple));
        var resolvedAgent = context.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp)
            ? rp!.Agent
            : projectConfig.Agent;
        var maxConcurrent = resolvedAgent.Parallelism.MaxConcurrentSkillRounds;
        var current = commands.First;
        var executionCount = 0;

        while (current is not null)
        {
            var batch = PeelBatch(current, maxConcurrent);
            if (executionCount + batch.Count > MaxCommandExecutions)
                return CommandResult.Fail(
                    $"Pipeline exceeded maximum of {MaxCommandExecutions} command executions. " +
                    "Possible infinite loop in command insertion.");

            var (result, advanceTo) = batch.Count == 1
                ? await ExecuteSingleStepAsync(
                    current, commands, projectConfig, context, ++executionCount, cancellationToken)
                : await ExecuteBatchStepAsync(
                    batch, commands, projectConfig, context, executionCount + 1, cancellationToken);

            if (batch.Count > 1) executionCount += batch.Count;

            if (!result.IsSuccess)
            {
                await TryPersistWorkBranchAsync(projectConfig, context, result, cancellationToken);
                lifecycle.MarkFailed();
                return result;
            }

            current = advanceTo ?? batch[^1].Next;
        }

        logger.LogInformation("Pipeline completed successfully");
        return CommandResult.Ok("Pipeline completed successfully");
    }

    private async Task<ISandbox?> TryCreateSandboxAsync(
        IReadOnlyList<string> commandNames, ProjectConfig projectConfig,
        PipelineContext context, CancellationToken cancellationToken)
    {
        if (!commandNames.Any(SandboxRequiringCommands.Contains)) return null;

        var projectMap = context.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var pm) ? pm : null;
        var spec = sandboxSpecBuilder.Build(projectConfig, projectMap);
        logger.LogInformation("Creating sandbox with toolchain image {Image}", spec.ToolchainImage);
        var sandbox = await sandboxFactory.CreateAsync(spec, cancellationToken);
        context.Set(ContextKeys.Sandbox, sandbox);
        return sandbox;
    }

    /// <summary>
    /// Best-effort persist of the WIP branch when the pipeline fails after producing
    /// local changes (p0112). Wrapped in its OWN try/catch so any persist exception
    /// can NEVER overwrite the original failure cause already in <paramref name="originalFailure"/>.
    /// </summary>
    private async Task TryPersistWorkBranchAsync(
        ProjectConfig projectConfig, PipelineContext context,
        CommandResult originalFailure, CancellationToken cancellationToken)
    {
        try
        {
            // Stamp the failed step name into context for the WIP commit's trailer block.
            context.Set(ContextKeys.FailedStepName, originalFailure.StepName);

            // Skip persist for source-less / discussion-style runs.
            if (!context.TryGet<AgentSmith.Domain.Entities.Repository>(ContextKeys.Repository, out _))
                return;

            var persistCmd = PipelineCommand.Simple(CommandNames.PersistWorkBranch);
            var persistContext = contextFactory.Create(persistCmd, projectConfig, context);
            var persistResult = await commandExecutor.ExecuteAsync(persistContext, cancellationToken);

            if (persistResult.IsSuccess)
                logger.LogInformation("Work branch persisted: {Message}", persistResult.Message);
            else
                logger.LogWarning("Work branch persist did not complete: {Message}", persistResult.Message);
        }
        catch (Exception ex)
        {
            // Never let a persist failure mask the original pipeline failure cause.
            logger.LogError(ex, "Work branch persist threw an exception — original failure cause preserved");
        }
    }

    internal static List<LinkedListNode<PipelineCommand>> PeelBatch(
        LinkedListNode<PipelineCommand> start, int maxConcurrent)
    {
        var batch = new List<LinkedListNode<PipelineCommand>> { start };
        if (maxConcurrent <= 1 || !IsBatchableCommand(start.Value.Name)) return batch;

        var probe = start.Next;
        while (probe is not null
               && probe.Value.Name == start.Value.Name
               && probe.Value.Round == start.Value.Round
               && IsBatchableCommand(probe.Value.Name))
        {
            batch.Add(probe);
            probe = probe.Next;
        }
        return batch;
    }

    private static bool IsBatchableCommand(string name) =>
        name is CommandNames.SkillRound
             or CommandNames.SecuritySkillRound
             or CommandNames.ApiSecuritySkillRound;

    private async Task<(CommandResult Result, LinkedListNode<PipelineCommand>? AdvanceTo)>
        ExecuteSingleStepAsync(
            LinkedListNode<PipelineCommand> current,
            LinkedList<PipelineCommand> commands,
            ProjectConfig projectConfig, PipelineContext context,
            int executionCount, CancellationToken cancellationToken)
    {
        var cmd = current.Value;
        var total = commands.Count;
        var label = CommandNames.GetLabel(cmd.Name);

        logger.LogInformation("[{Step}/{Total}] Executing {Command}...",
            executionCount, total, cmd.DisplayName);
        await progressReporter.ReportProgressAsync(executionCount, total, label, cancellationToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await SafeExecuteAsync(cmd, projectConfig, context, cancellationToken);
        sw.Stop();

        context.TrackCommand(cmd.DisplayName, result.IsSuccess, result.Message,
            sw.Elapsed, result.InsertNext?.Count);

        if (!result.IsSuccess)
        {
            await ReportFailureAsync(executionCount, total, label, cmd, result,
                projectConfig, context, cancellationToken);
            return (result with { FailedStep = executionCount, TotalSteps = total, StepName = label }, null);
        }

        InsertFollowUps(current, commands, result);
        await PostSkillDetailAsync(cmd, result, cancellationToken);
        logger.LogInformation("[{Step}/{Total}] {Command} completed: {Message}",
            executionCount, commands.Count, cmd.DisplayName, result.Message);
        return (result, null);
    }

    private async Task<(CommandResult Result, LinkedListNode<PipelineCommand>? AdvanceTo)>
        ExecuteBatchStepAsync(
            IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
            LinkedList<PipelineCommand> commands,
            ProjectConfig projectConfig, PipelineContext context,
            int firstStepIndex, CancellationToken cancellationToken)
    {
        var runner = new PipelineBatchRunner(commandExecutor, contextFactory, progressReporter, logger);
        var outcome = await runner.ExecuteAsync(
            batch, projectConfig, context, firstStepIndex, commands.Count, cancellationToken);

        TrackBatchedCommands(outcome, context);

        var failure = outcome.FirstFailure();
        if (failure is not null)
        {
            await ReportFailureAsync(failure.StepIndex, commands.Count,
                CommandNames.GetLabel(failure.Command.Name), failure.Command, failure.Result,
                projectConfig, context, cancellationToken);
            return (failure.Result with
            {
                FailedStep = failure.StepIndex,
                TotalSteps = commands.Count,
                StepName = CommandNames.GetLabel(failure.Command.Name)
            }, null);
        }

        var firstInsert = outcome.FirstInsertNext();
        if (firstInsert is not null)
            InsertFollowUps(firstInsert.Value.Node, commands, firstInsert.Value.Result);

        await PostBatchSkillDetailsAsync(outcome, cancellationToken);
        return (CommandResult.Ok(
            $"Batch of {batch.Count} {batch[0].Value.Name} skills (round {batch[0].Value.Round}) completed"), null);
    }

    private async Task<CommandResult> SafeExecuteAsync(
        PipelineCommand cmd, ProjectConfig projectConfig, PipelineContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var commandContext = contextFactory.Create(cmd, projectConfig, context);
            return await commandExecutor.ExecuteAsync(commandContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} threw an unhandled exception", cmd.DisplayName);
            return CommandResult.Fail($"{cmd.DisplayName} failed: {ex.Message}");
        }
    }

    private static void TrackBatchedCommands(BatchOutcome outcome, PipelineContext context)
    {
        foreach (var slot in outcome.Slots)
        {
            if (slot is null) continue;
            context.TrackCommand(slot.Command.DisplayName, slot.Result.IsSuccess,
                slot.Result.Message, slot.Elapsed, slot.Result.InsertNext?.Count);
        }
    }

    private async Task PostBatchSkillDetailsAsync(BatchOutcome outcome, CancellationToken ct)
    {
        foreach (var slot in outcome.Slots)
        {
            if (slot is null) continue;
            await PostSkillDetailAsync(slot.Command, slot.Result, ct);
        }
    }

    private async Task ReportFailureAsync(
        int executionCount, int total, string label,
        PipelineCommand cmd, CommandResult result,
        ProjectConfig projectConfig, PipelineContext context, CancellationToken ct)
    {
        logger.LogWarning("Pipeline stopped at step {Step}: {Command} failed - {Message}",
            executionCount, cmd.DisplayName, result.Message);
        // HTML-formatted: AzDO System.History accepts HTML; GitHub/GitLab markdown comments
        // render inline HTML; only Jira's ADF flattens it to plain text (acceptable fallback).
        var safeMessage = System.Net.WebUtility.HtmlEncode(result.Message ?? "");
        await PostTicketStatusAsync(projectConfig, context,
            $"<b>Agent Smith — Failed</b><br/>" +
            $"<b>Step:</b> {System.Net.WebUtility.HtmlEncode(label)} ({executionCount}/{total})<br/>" +
            $"<b>Error:</b> {safeMessage}", ct);
    }

    private void InsertFollowUps(
        LinkedListNode<PipelineCommand> after,
        LinkedList<PipelineCommand> commands,
        CommandResult result)
    {
        if (result.InsertNext is not { Count: > 0 } follow) return;

        var insertAfter = after;
        foreach (var next in follow)
        {
            commands.AddAfter(insertAfter, next);
            insertAfter = insertAfter.Next!;
        }
        logger.LogInformation("{Command} inserted {Count} follow-up commands: {Commands}",
            after.Value.DisplayName, follow.Count, string.Join(", ", follow));
    }

    private async Task PostSkillDetailAsync(
        PipelineCommand cmd, CommandResult result, CancellationToken cancellationToken)
    {
        try
        {
            var detail = cmd.Name switch
            {
                CommandNames.Triage
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

}
