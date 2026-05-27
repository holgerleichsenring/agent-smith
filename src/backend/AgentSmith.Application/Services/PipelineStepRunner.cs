using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Owns single-step + batched-step dispatch through the CommandExecutor.
///
/// Concerns kept here:
///   - context-factory call + CommandExecutor dispatch
///   - per-step exception envelope (catch-all → CommandResult.Fail; OCE rethrows)
///   - progress reporter "progress" event
///   - command tracking (PipelineContext.TrackCommand)
///   - DataFlow read-gate attach
///   - dynamic-command insertion via CommandResult.InsertNext
///   - per-step skill-detail emission (Triage / SkillRound / ConvergenceCheck / SwitchSkill)
///
/// Concerns explicitly NOT here:
///   - sandbox lifecycle (IPipelineSandboxCoordinator)
///   - failure ticket-posting + WIP-persist + lifecycle marking (IPipelineErrorHandler)
///   - pipeline-level orchestration loop (PipelineExecutor)
/// </summary>
public sealed class PipelineStepRunner(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    IProgressReporter progressReporter,
    DataFlowReadGate dataFlowReadGate,
    ISkillRoundBufferDispatcher bufferDispatcher,
    IEventPublisher eventPublisher,
    ILogger<PipelineStepRunner> logger) : IPipelineStepRunner
{
    public async Task<StepExecutionResult> RunSingleAsync(
        LinkedListNode<PipelineCommand> current,
        LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig,
        PipelineContext context,
        int executionCount,
        CancellationToken cancellationToken)
    {
        var cmd = current.Value;
        var total = commands.Count;
        var label = CommandNames.GetLabel(cmd.Name);

        logger.LogInformation("[{Step}/{Total}] Executing {Command}...",
            executionCount, total, cmd.DisplayName);
        await progressReporter.ReportProgressAsync(executionCount, total, label, cancellationToken);
        await PublishStepStartedAsync(context, executionCount, label, total, cancellationToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        context.Set(ContextKeys.ActivePhaseStep, cmd.Name);
        using (AttachReadGate(cmd.Name, context))
        {
            var result = await SafeExecuteAsync(cmd, projectConfig, context, cancellationToken);
            sw.Stop();
            await PublishStepFinishedAsync(
                context, executionCount,
                result.IsSuccess ? "success" : "failed",
                sw.ElapsedMilliseconds,
                result.IsSuccess ? null : result.Message,
                cancellationToken);
            return await FinalizeStepAsync(
                current, commands, context, executionCount, cmd, label, sw.Elapsed, result, cancellationToken);
        }
    }

    public async Task<StepExecutionResult> RunBatchAsync(
        IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
        LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig,
        PipelineContext context,
        int firstStepIndex,
        CancellationToken cancellationToken)
    {
        var batchLabel = $"{CommandNames.GetLabel(batch[0].Value.Name)} batch×{batch.Count}";
        await PublishStepStartedAsync(context, firstStepIndex, batchLabel, commands.Count, cancellationToken);
        var batchSw = System.Diagnostics.Stopwatch.StartNew();
        var runner = new PipelineBatchRunner(commandExecutor, contextFactory, progressReporter, bufferDispatcher, logger);
        var outcome = await runner.ExecuteAsync(
            batch, projectConfig, context, firstStepIndex, commands.Count, cancellationToken);
        batchSw.Stop();
        var firstFailureSlot = outcome.FirstFailure();
        var anyFailed = firstFailureSlot is not null;
        await PublishStepFinishedAsync(
            context, firstStepIndex,
            anyFailed ? "failed" : "success",
            batchSw.ElapsedMilliseconds,
            anyFailed ? firstFailureSlot!.Result.Message : null,
            cancellationToken);

        TrackBatchedCommands(outcome, context);

        if (firstFailureSlot is not null)
        {
            return new StepExecutionResult(
                firstFailureSlot.Result with
                {
                    FailedStep = firstFailureSlot.StepIndex,
                    TotalSteps = commands.Count,
                    StepName = CommandNames.GetLabel(firstFailureSlot.Command.Name)
                },
                null);
        }

        var firstInsert = outcome.FirstInsertNext();
        if (firstInsert is not null)
            InsertFollowUps(firstInsert.Value.Node, commands, firstInsert.Value.Result);

        await PostBatchSkillDetailsAsync(outcome, cancellationToken);
        return new StepExecutionResult(
            CommandResult.Ok(
                $"Batch of {batch.Count} {batch[0].Value.Name} skills (round {batch[0].Value.Round}) completed"),
            null);
    }

    public IReadOnlyList<LinkedListNode<PipelineCommand>> PeelBatch(
        LinkedListNode<PipelineCommand> start, int maxConcurrent)
        => PeelBatchInternal(start, maxConcurrent);

    internal static List<LinkedListNode<PipelineCommand>> PeelBatchInternal(
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

    internal static bool IsBatchableCommand(string name) =>
        name is CommandNames.SkillRound
             or CommandNames.SecuritySkillRound
             or CommandNames.ApiSecuritySkillRound;

    private async Task<CommandResult> SafeExecuteAsync(
        PipelineCommand cmd, ResolvedProject projectConfig, PipelineContext context,
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

    private async Task<StepExecutionResult> FinalizeStepAsync(
        LinkedListNode<PipelineCommand> current, LinkedList<PipelineCommand> commands,
        PipelineContext context, int executionCount,
        PipelineCommand cmd, string label, TimeSpan elapsed, CommandResult result,
        CancellationToken cancellationToken)
    {
        var total = commands.Count;

        context.TrackCommand(cmd.DisplayName, result.IsSuccess, result.Message,
            elapsed, result.InsertNext?.Count);

        if (!result.IsSuccess)
        {
            return new StepExecutionResult(
                result with { FailedStep = executionCount, TotalSteps = total, StepName = label },
                null);
        }

        InsertFollowUps(current, commands, result);
        await PostSkillDetailAsync(cmd, result, cancellationToken);
        logger.LogInformation("[{Step}/{Total}] {Command} completed: {Message}",
            executionCount, commands.Count, cmd.DisplayName, result.Message);
        return new StepExecutionResult(result, null);
    }

    private IDisposable? AttachReadGate(string activeStep, PipelineContext context)
    {
        var resolved = context.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp)
            ? rp
            : null;
        return resolved is null
            ? null
            : dataFlowReadGate.AttachToStep(activeStep, resolved.PipelineName, context);
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

    private Task PublishStepStartedAsync(
        PipelineContext context, int stepIndex, string stepName, int totalSteps, CancellationToken ct)
    {
        if (!context.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new StepStartedEvent(runId, stepIndex, stepName, totalSteps, DateTimeOffset.UtcNow), ct);
    }

    private Task PublishStepFinishedAsync(
        PipelineContext context, int stepIndex, string status, long durationMs, string? reason, CancellationToken ct)
    {
        if (!context.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new StepFinishedEvent(runId, stepIndex, status, durationMs, DateTimeOffset.UtcNow, reason), ct);
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
}
