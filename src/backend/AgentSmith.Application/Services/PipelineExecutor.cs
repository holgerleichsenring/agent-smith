using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Thin orchestrator: iterates commands, delegates each step to
/// <see cref="IPipelineStepRunner"/>, lazy-boots the sandbox via
/// <see cref="IPipelineSandboxCoordinator"/>, and routes failures through
/// <see cref="IPipelineErrorHandler"/>. Dynamic-command insertion lives here
/// because mutating the LinkedList is part of the iteration logic itself.
/// </summary>
public sealed class PipelineExecutor(
    IServiceProvider serviceProvider,
    IPipelineStepRunner stepRunner,
    IPipelineErrorHandler errorHandler,
    IPipelineLifecycleCoordinator lifecycleCoordinator,
    ILogger<PipelineExecutor> logger) : IPipelineExecutor
{
    private const int MaxCommandExecutions = 100;

    public async Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames, ResolvedProject projectConfig,
        PipelineContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting pipeline with {Count} commands", commandNames.Count);
        for (var i = 0; i < commandNames.Count; i++)
            logger.LogInformation("  [{Index}/{Total}] {Command}", i + 1, commandNames.Count, commandNames[i]);

        await errorHandler.PostWorkingStatusAsync(projectConfig, context, cancellationToken);
        await using var lifecycle = await lifecycleCoordinator.BeginAsync(projectConfig, context, cancellationToken);
        await using var sandbox = serviceProvider.GetRequiredService<IPipelineSandboxCoordinator>();
        try { return await RunLoopAsync(commandNames, projectConfig, context, lifecycle, sandbox, cancellationToken); }
        catch { lifecycle.MarkFailed(); throw; }
    }

    private async Task<CommandResult> RunLoopAsync(
        IReadOnlyList<string> commandNames, ResolvedProject projectConfig, PipelineContext context,
        IAsyncPipelineLifecycle lifecycle, IPipelineSandboxCoordinator sandbox, CancellationToken ct)
    {
        var commands = new LinkedList<PipelineCommand>(commandNames.Select(PipelineCommand.Simple));
        var maxConcurrent = PipelineExecutorPolicy.ResolveMaxConcurrent(projectConfig, context);
        var current = commands.First;
        var executionCount = 0;

        while (current is not null)
        {
            var batch = stepRunner.PeelBatch(current, maxConcurrent);
            if (batch.Any(n => sandbox.IsSandboxRequiring(n.Value.Name)))
                await sandbox.EnsureSandboxesAsync(projectConfig, context, ct);

            if (executionCount + batch.Count > MaxCommandExecutions)
            {
                lifecycle.MarkFailed();
                return CommandResult.Fail($"Pipeline exceeded maximum of {MaxCommandExecutions} command executions. " +
                                          "Possible infinite loop in command insertion.");
            }

            var stepResult = batch.Count == 1
                ? await stepRunner.RunSingleAsync(current, commands, projectConfig, context, ++executionCount, ct)
                : await stepRunner.RunBatchAsync(batch, commands, projectConfig, context, executionCount + 1, ct);
            if (batch.Count > 1) executionCount += batch.Count;

            if (!stepResult.Result.IsSuccess)
            {
                // p0237: a failed step used to short-circuit straight to the
                // error handler, skipping the finalizer tail (WriteRunResult,
                // CommitAndPR, …). A run then "failed" with no result.md, no
                // record PR, and only a bare reason in the ticket. Now run the
                // remaining finalizers anyway so a failed/cancelled run still
                // records WHY + opens a record PR. PersistWorkBranch (partial
                // work) stays with the error handler.
                var original = stepResult.Result;
                var failure = CommandResult.Fail(NormalizeReason(original.Message), original.Exception) with
                {
                    FailedStep = original.FailedStep,
                    TotalSteps = original.TotalSteps,
                    StepName = original.StepName,
                };
                context.Set(ContextKeys.FailureReason, failure.Message);
                await RunFinalizerTailAsync(batch[^1], commands, projectConfig, context, executionCount, ct);
                await errorHandler.HandleStepFailureAsync(commandNames, projectConfig, context, lifecycle, failure, ct);
                return failure;
            }
            if (PipelineExecutorPolicy.TryGetParkedReason(context, logger, out var parked)) return CommandResult.Ok(parked);
            current = stepResult.AdvanceTo ?? batch[^1].Next;
        }

        logger.LogInformation("Pipeline completed successfully");
        return CommandResult.Ok("Pipeline completed successfully");
    }

    // p0237: commands that must run even when an earlier step failed, so a
    // failed/cancelled run still produces a record. PersistWorkBranch is NOT
    // here — the error handler owns the best-effort WIP push (its own guard for
    // read-only pipelines). These run AFTER the failed step, in pipeline order.
    private static readonly HashSet<string> FinalizerCommands = new(StringComparer.Ordinal)
    {
        CommandNames.WriteRunResult,
        CommandNames.CommitAndPR,
        CommandNames.PrCrossLink,
    };

    // p0237: the LLM SDK's NetworkTimeout surfaces, through several layers, as
    // the useless ".NET ".Message" "A task was canceled." Whichever handler
    // produced it, normalise that single phrase into the actionable lever so the
    // operator never reads a bare "canceled" in result.md / the ticket again.
    // (Operator/watchdog cancels never reach here — they propagate to p0232.)
    private static string NormalizeReason(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "unknown";
        if (message.Contains("task was cancel", StringComparison.OrdinalIgnoreCase))
            return message.TrimEnd('.', ' ')
                + " — the LLM request timed out at the HTTP layer (SDK NetworkTimeout). "
                + "Raise the agent's network_timeout_seconds (default 300s) if this recurs.";
        return message;
    }

    private async Task RunFinalizerTailAsync(
        LinkedListNode<PipelineCommand> failedNode, LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig, PipelineContext context, int executionCount, CancellationToken ct)
    {
        for (var node = failedNode.Next; node is not null; node = node.Next)
        {
            if (!FinalizerCommands.Contains(node.Value.Name)) continue;
            try
            {
                // Best-effort: a finalizer that throws/fails must not stop the
                // others — a failed run still records as much as it can.
                await stepRunner.RunSingleAsync(node, commands, projectConfig, context, ++executionCount, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Finalizer {Command} threw while finalizing a failed run", node.Value.Name);
            }
        }
    }
}
