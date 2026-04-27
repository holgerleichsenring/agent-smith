using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Runs a batch of consecutive same-(Name, Round) skill-round commands in parallel
/// under a SemaphoreSlim throttle. Captures per-skill outputs in DeferredBuffers and
/// merges them into the pipeline context in deterministic skill-graph order on completion.
/// Fail-fast: first failure cancels the rest of the batch via a linked CTS.
/// </summary>
public sealed class PipelineBatchRunner(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    IProgressReporter progressReporter,
    ILogger logger)
{
    public async Task<BatchOutcome> ExecuteAsync(
        IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
        ProjectConfig projectConfig,
        PipelineContext context,
        int firstStepIndex,
        int totalSteps,
        CancellationToken cancellationToken)
    {
        var deferred = new List<SkillRoundBuffer>();
        context.Set(ContextKeys.DeferredBuffers, deferred);

        var maxConcurrent = projectConfig.Agent.Parallelism.MaxConcurrentSkillRounds;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var throttle = new SemaphoreSlim(maxConcurrent);

        var slots = new BatchSlot[batch.Count];

        try
        {
            var tasks = batch.Select((node, i) => RunSlotAsync(
                node, projectConfig, context, firstStepIndex + i, totalSteps,
                throttle, linkedCts, slots, i)).ToArray();
            try { await Task.WhenAll(tasks); } catch { /* aggregated below */ }
        }
        finally
        {
            context.Set(ContextKeys.DeferredBuffers, new List<SkillRoundBuffer>());
        }

        MergeBuffersInGraphOrder(batch, deferred, context);

        return new BatchOutcome(slots, batch, firstStepIndex);
    }

    private async Task RunSlotAsync(
        LinkedListNode<PipelineCommand> node, ProjectConfig projectConfig, PipelineContext context,
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
        PipelineCommand cmd, ProjectConfig projectConfig, PipelineContext context,
        int stepIndex, int totalSteps, CancellationToken ct)
    {
        logger.LogInformation("[{Step}/{Total}] Executing {Command}...",
            stepIndex, totalSteps, cmd.DisplayName);
        await progressReporter.ReportProgressAsync(
            stepIndex, totalSteps, CommandNames.GetLabel(cmd.Name), ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await SafeExecuteAsync(cmd, projectConfig, context, ct);
        sw.Stop();
        return new BatchSlot(cmd, result, sw.Elapsed, stepIndex);
    }

    private async Task<CommandResult> SafeExecuteAsync(
        PipelineCommand cmd, ProjectConfig projectConfig, PipelineContext context,
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

    private static void MergeBuffersInGraphOrder(
        IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
        IReadOnlyList<SkillRoundBuffer> deferred,
        PipelineContext context)
    {
        var bySkill = deferred
            .GroupBy(b => b.SkillName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var node in batch)
        {
            if (node.Value.SkillName is null) continue;
            if (!bySkill.TryGetValue(node.Value.SkillName, out var buffers)) continue;

            foreach (var buffer in buffers)
                SkillRoundHandlerBase.ApplyBufferToContext(context, buffer);
        }
    }
}
