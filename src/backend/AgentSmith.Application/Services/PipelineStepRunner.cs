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
        var label = ComposeStepLabel(cmd);

        logger.LogInformation("[{Step}/{Total}] Executing {Command}...",
            executionCount, total, cmd.DisplayName);
        await progressReporter.ReportProgressAsync(executionCount, total, cmd, cancellationToken);
        await PublishStepStartedAsync(context, executionCount, label, total,
            ComposeDisplayName(cmd), cancellationToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        context.Set(ContextKeys.ActivePhaseStep, cmd.Name);
        using (AttachReadGate(cmd.Name, context))
        {
            var result = await SafeExecuteAsync(cmd, projectConfig, context, cancellationToken);
            sw.Stop();
            // p0203: pass result.Message on success too so the execution-tree
            // can render the handler's one-line outcome under the step row
            // instead of bare "done". Failure path unchanged — Message IS
            // the failure reason there.
            await PublishStepFinishedAsync(
                context, executionCount,
                result.IsSuccess ? "success" : "failed",
                sw.ElapsedMilliseconds,
                result.Message,
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
        var batchDisplay = $"{CommandDisplayNames.Get(batch[0].Value.Name)} batch×{batch.Count}";
        await PublishStepStartedAsync(context, firstStepIndex, batchLabel, commands.Count,
            batchDisplay, cancellationToken);
        var batchSw = System.Diagnostics.Stopwatch.StartNew();
        var runner = new PipelineBatchRunner(commandExecutor, contextFactory, progressReporter, bufferDispatcher, logger);
        var outcome = await runner.ExecuteAsync(
            batch, projectConfig, context, firstStepIndex, commands.Count, cancellationToken);
        batchSw.Stop();
        var firstFailureSlot = outcome.FirstFailure();
        var anyFailed = firstFailureSlot is not null;
        // p0203: on a successful batch surface a synthetic "Batch of N
        // {command} completed" message so the row doesn't render as a bare
        // "done". On failure surface the first failure's message (existing
        // shape).
        var batchMessage = anyFailed
            ? firstFailureSlot!.Result.Message
            : $"Batch of {batch.Count} {batch[0].Value.Name} skills completed";
        await PublishStepFinishedAsync(
            context, firstStepIndex,
            anyFailed ? "failed" : "success",
            batchSw.ElapsedMilliseconds,
            batchMessage,
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

        await PostBatchSkillDetailsAsync(outcome, context, cancellationToken);
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
        // Operator/watchdog cancel (our run token IS cancelled) → propagate so
        // the pipeline-level p0232 handler maps the operator/watchdog reason.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} threw an unhandled exception", cmd.DisplayName);
            return CommandResult.Fail($"{cmd.DisplayName} failed: {DescribeStepException(ex)}");
        }
    }

    // p0237: an OperationCanceledException whose run token is NOT cancelled (it
    // didn't match the guard above) is an internal LLM-layer timeout (the SDK's
    // NetworkTimeout), not an operator cancel — the raw ".NET ".Message" is the
    // useless "A task was canceled.". Name the actual lever for ANY step that
    // makes LLM calls (AnalyzeCode, the master, …), not just the master handler.
    private static string DescribeStepException(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is OperationCanceledException)
                return "cut off by an internal timeout. If a build/test command was running "
                    + "it likely exceeded sandbox.run_command_timeout_seconds; if an LLM call "
                    + "stalled, raise the agent's network_timeout_seconds (default 300s).";
        }
        return ex.Message;
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
        await PostSkillDetailAsync(cmd, result, executionCount, context, cancellationToken);
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

    private async Task PostBatchSkillDetailsAsync(BatchOutcome outcome, PipelineContext context, CancellationToken ct)
    {
        foreach (var slot in outcome.Slots)
        {
            if (slot is null) continue;
            await PostSkillDetailAsync(slot.Command, slot.Result, slot.StepIndex, context, ct);
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
        PipelineContext context, int stepIndex, string stepName, int totalSteps,
        string? displayName, CancellationToken ct)
    {
        if (!context.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new StepStartedEvent(runId, stepIndex, stepName, totalSteps, DateTimeOffset.UtcNow, displayName), ct);
    }

    // p0176c: step-name composition appends a (repo, component) suffix when
    // the PipelineCommand carries RepoName / ContextName so multi-repo
    // BootstrapRound dispatches render as one operator-readable row per
    // (repo, component) pair instead of N identical "Producing bootstrap
    // files" rows. The base label still comes from CommandNames.GetLabel.
    internal static string ComposeStepLabel(PipelineCommand cmd)
    {
        var label = CommandNames.GetLabel(cmd.Name);
        var hasRepo = !string.IsNullOrEmpty(cmd.RepoName);
        var hasContext = !string.IsNullOrEmpty(cmd.ContextName);
        if (!hasRepo && !hasContext) return label;
        if (hasRepo && hasContext) return $"{label} ({cmd.RepoName}, {cmd.ContextName})";
        if (hasRepo) return $"{label} ({cmd.RepoName})";
        return $"{label} ({cmd.ContextName})";
    }

    // p0203: operator-facing display name composition. Mirrors the
    // ComposeStepLabel suffix logic but draws the base label from
    // CommandDisplayNames (noun-phrase) instead of CommandNames.GetLabel
    // (present-continuous).
    internal static string ComposeDisplayName(PipelineCommand cmd)
    {
        var label = CommandDisplayNames.Get(cmd.Name);
        var hasRepo = !string.IsNullOrEmpty(cmd.RepoName);
        var hasContext = !string.IsNullOrEmpty(cmd.ContextName);
        if (!hasRepo && !hasContext) return label;
        if (hasRepo && hasContext) return $"{label} ({cmd.RepoName}, {cmd.ContextName})";
        if (hasRepo) return $"{label} ({cmd.RepoName})";
        return $"{label} ({cmd.ContextName})";
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
        PipelineCommand cmd, CommandResult result, int stepIndex, PipelineContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var (origin, detail) = cmd.Name switch
            {
                CommandNames.Triage
                    => ("triage", result.Message),
                CommandNames.SkillRound or CommandNames.SecuritySkillRound or CommandNames.ApiSecuritySkillRound
                    => ("skill-round", result.Message),
                CommandNames.ConvergenceCheck => ("convergence", result.Message),
                CommandNames.SwitchSkill => ("skill-switch", result.Message),
                _ => (null, null)
            };

            if (origin is null || detail is null) return;
            if (!context.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
                return;

            await eventPublisher.PublishAsync(
                new L1StepDetailEvent(runId, stepIndex, origin, detail, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to post skill detail");
        }
    }
}
