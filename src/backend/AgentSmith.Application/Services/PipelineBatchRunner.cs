using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Runs a batch of consecutive same-(Name, Round) commands in parallel under a
/// SemaphoreSlim throttle. Fail-fast: first failure cancels the rest of the batch
/// via a linked CTS.
///
/// p0312a removed the deferred-buffer merge. Buffers existed so parallel skill
/// rounds could write their outputs into the shared context in deterministic
/// skill-graph order rather than in completion order; with the SkillRound family
/// gone nothing produces one, so the merge iterated an always-empty list.
/// </summary>
public sealed class PipelineBatchRunner(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    IProgressReporter progressReporter,
    ILogger logger)
{
    public async Task<BatchOutcome> ExecuteAsync(
        IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
        ResolvedProject projectConfig,
        PipelineContext context,
        int firstStepIndex,
        int totalSteps,
        CancellationToken cancellationToken)
    {
        var maxConcurrent = projectConfig.Agent.Parallelism.MaxConcurrentSkillRounds;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var throttle = new SemaphoreSlim(maxConcurrent);

        var slots = new BatchSlot[batch.Count];

        var tasks = batch.Select((node, i) => RunSlotAsync(
            node, projectConfig, context, firstStepIndex + i, totalSteps,
            throttle, linkedCts, slots, i)).ToArray();
        try { await Task.WhenAll(tasks); } catch { /* aggregated below */ }

        return new BatchOutcome(slots, batch, firstStepIndex);
    }

    private async Task RunSlotAsync(
        LinkedListNode<PipelineCommand> node, ResolvedProject projectConfig, PipelineContext context,
        int stepIndex, int totalSteps, SemaphoreSlim throttle, CancellationTokenSource linkedCts,
        BatchSlot[] slots, int slot)
    {
        await throttle.WaitAsync(linkedCts.Token);
        try
        {
            slots[slot] = await ExecuteOneAsync(
                node.Value, projectConfig, context, stepIndex, totalSteps, linkedCts.Token);
            if (!slots[slot].Result.IsSuccess) linkedCts.Cancel();
        }
        finally
        {
            throttle.Release();
        }
    }

    private async Task<BatchSlot> ExecuteOneAsync(
        PipelineCommand cmd, ResolvedProject projectConfig, PipelineContext context,
        int stepIndex, int totalSteps, CancellationToken ct)
    {
        logger.LogInformation("[{Step}/{Total}] Executing {Command}...",
            stepIndex, totalSteps, cmd.DisplayName);
        await progressReporter.ReportProgressAsync(stepIndex, totalSteps, cmd, ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await SafeExecuteAsync(cmd, projectConfig, context, ct);
        sw.Stop();
        return new BatchSlot(cmd, result, sw.Elapsed, stepIndex);
    }

    private async Task<CommandResult> SafeExecuteAsync(
        PipelineCommand cmd, ResolvedProject projectConfig, PipelineContext context,
        CancellationToken ct)
    {
        try
        {
            var commandContext = contextFactory.Create(cmd, projectConfig, context);
            return await commandExecutor.ExecuteAsync(commandContext, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} threw an unhandled exception", cmd.DisplayName);
            return CommandResult.Fail($"{cmd.DisplayName} failed: {ex.Message}");
        }
    }

}
