using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Thin orchestrator (p0147e rewrite). Walks the (possibly mid-flight extended)
/// command list, delegates each step to <see cref="IPipelineStepRunner"/>,
/// hands failures to <see cref="IPipelineErrorHandler"/>, and asks
/// <see cref="IPipelineSandboxCoordinator"/> to lazy-boot the sandbox on first
/// sandbox-requiring step.
///
/// Dynamic-command-insertion (TriageCommand returning follow-ups via
/// CommandResult.OkAndContinueWith) stays here because mutating the LinkedList
/// is part of the iteration logic itself.
///
/// Compatibility wrapper for tests: exposes <see cref="PeelBatch"/> as a static
/// pass-through to <see cref="PipelineStepRunner.PeelBatchInternal"/> so the
/// existing batching test suite keeps compiling without touching call sites.
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
        IReadOnlyList<string> commandNames,
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting pipeline with {Count} commands", commandNames.Count);
        for (var i = 0; i < commandNames.Count; i++)
            logger.LogInformation("  [{Index}/{Total}] {Command}",
                i + 1, commandNames.Count, commandNames[i]);

        await errorHandler.PostWorkingStatusAsync(projectConfig, context, cancellationToken);

        await using var lifecycle = await lifecycleCoordinator.BeginAsync(projectConfig, context, cancellationToken);
        await using var sandbox = serviceProvider.GetRequiredService<IPipelineSandboxCoordinator>();
        try
        {
            return await RunLoopAsync(commandNames, projectConfig, context, lifecycle, sandbox, cancellationToken);
        }
        catch
        {
            // Setup exceptions (sandbox boot) or escapes from the loop must mark the
            // lifecycle failed — otherwise dispose transitions the ticket to Done
            // instead of Failed and the operator never learns the run broke.
            lifecycle.MarkFailed();
            throw;
        }
    }

    private async Task<CommandResult> RunLoopAsync(
        IReadOnlyList<string> commandNames,
        ResolvedProject projectConfig,
        PipelineContext context,
        IAsyncPipelineLifecycle lifecycle,
        IPipelineSandboxCoordinator sandbox,
        CancellationToken cancellationToken)
    {
        var commands = new LinkedList<PipelineCommand>(commandNames.Select(PipelineCommand.Simple));
        var maxConcurrent = ResolveMaxConcurrent(projectConfig, context);
        var current = commands.First;
        var executionCount = 0;

        while (current is not null)
        {
            var batch = stepRunner.PeelBatch(current, maxConcurrent);

            // Lazy sandbox creation: build it only when the first sandbox-requiring
            // command in the batch comes up.
            if (batch.Any(n => sandbox.IsSandboxRequiring(n.Value.Name)))
                await sandbox.EnsureSandboxAsync(projectConfig, context, cancellationToken);

            if (executionCount + batch.Count > MaxCommandExecutions)
            {
                var overflow = CommandResult.Fail(
                    $"Pipeline exceeded maximum of {MaxCommandExecutions} command executions. " +
                    "Possible infinite loop in command insertion.");
                lifecycle.MarkFailed();
                return overflow;
            }

            var stepResult = batch.Count == 1
                ? await stepRunner.RunSingleAsync(current, commands, projectConfig, context, ++executionCount, cancellationToken)
                : await stepRunner.RunBatchAsync(batch, commands, projectConfig, context, executionCount + 1, cancellationToken);

            if (batch.Count > 1) executionCount += batch.Count;

            if (!stepResult.Result.IsSuccess)
            {
                await errorHandler.HandleStepFailureAsync(
                    commandNames, projectConfig, context, lifecycle, stepResult.Result, cancellationToken);
                return stepResult.Result;
            }

            if (TryGetParkedReason(context, out var parkedMessage))
                return CommandResult.Ok(parkedMessage);

            current = stepResult.AdvanceTo ?? batch[^1].Next;
        }

        logger.LogInformation("Pipeline completed successfully");
        return CommandResult.Ok("Pipeline completed successfully");
    }

    private static int ResolveMaxConcurrent(ResolvedProject projectConfig, PipelineContext context)
    {
        var resolvedAgent = context.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp)
            ? rp!.Agent
            : projectConfig.Agent;
        return resolvedAgent.Parallelism.MaxConcurrentSkillRounds;
    }

    private bool TryGetParkedReason(PipelineContext context, out string message)
    {
        // p0128b: PlanOpenQuestionsHandler sets OpenQuestionsAwaitingAnswer when the
        // Plan emitted needs_user_input. Halt cleanly — parked until operator reply.
        if (context.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out var awaiting) && awaiting)
        {
            logger.LogInformation("Pipeline parked: Plan emitted open questions; waiting on operator reply");
            message = "Pipeline parked: awaiting_user_input";
            return true;
        }
        // p0140e: EmptyPlanCheckHandler sets EmptyPlanSkipped when the Plan produced
        // zero steps. Cleanly skip the remaining handlers — gate emitted the counter.
        if (context.TryGet<bool>(ContextKeys.EmptyPlanSkipped, out var emptyPlanSkipped) && emptyPlanSkipped)
        {
            logger.LogInformation("Pipeline skipped: Plan produced zero steps (empty_plan)");
            message = "Pipeline skipped: empty_plan";
            return true;
        }
        message = string.Empty;
        return false;
    }

    /// <summary>
    /// Test-shim: legacy <c>PipelineExecutorBatchingTests</c> calls
    /// <c>PipelineExecutor.PeelBatch</c> directly. Delegates to the new
    /// step-runner's internal peeler so the existing assertions keep working
    /// without touching call sites.
    /// </summary>
    internal static List<LinkedListNode<PipelineCommand>> PeelBatch(
        LinkedListNode<PipelineCommand> start, int maxConcurrent)
        => PipelineStepRunner.PeelBatchInternal(start, maxConcurrent);
}
