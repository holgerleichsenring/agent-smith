using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// p0237: the commands that must run even when an earlier step failed, so a
/// failed or cancelled run still produces a record. PersistWorkBranch is NOT here
/// — the error handler owns the best-effort WIP push (its own guard for read-only
/// pipelines). These run AFTER the failed step, in pipeline order.
/// <para>
/// p0405: split out of PipelineExecutor, which iterates the pipeline; deciding
/// what still has to happen once iteration has stopped is a different job.
/// </para>
/// </summary>
public sealed class PipelineFinalizerTail(
    IPipelineStepRunner stepRunner, ILogger<PipelineFinalizerTail> logger)
{
    private static readonly HashSet<string> FinalizerCommands = new(StringComparer.Ordinal)
    {
        CommandNames.WriteRunResult,
        CommandNames.CommitAndPR,
        CommandNames.PrCrossLink,
    };

    public async Task RunAsync(
        LinkedListNode<PipelineCommand> failedNode, LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig, PipelineContext context, int executionCount, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(failedNode);
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
