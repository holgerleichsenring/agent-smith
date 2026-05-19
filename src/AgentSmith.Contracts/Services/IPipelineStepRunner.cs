using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Executes a single pipeline step (or a parallel batch of consecutive
/// same-(Name, Round) skill-round steps) through the CommandExecutor.
///
/// Responsibility (one): pull the CommandContext from the factory, dispatch via
/// CommandExecutor, emit progress events, capture timing, and surface the
/// per-command exception envelope (catch-all → CommandResult.Fail). Iteration,
/// sandbox lifecycle, lifecycle marking, and dynamic-command insertion are NOT
/// this service's concern.
///
/// p0147e extraction from PipelineExecutor.
/// </summary>
public interface IPipelineStepRunner
{
    /// <summary>
    /// Run a single command. Per-command exceptions (other than
    /// <see cref="OperationCanceledException"/>) are caught and wrapped in
    /// <see cref="CommandResult.Fail"/>; OCE propagates.
    /// </summary>
    Task<StepExecutionResult> RunSingleAsync(
        LinkedListNode<PipelineCommand> current,
        LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig,
        PipelineContext context,
        int executionCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// Run a parallel batch of skill-round commands (peeled by
    /// <see cref="IPipelineStepRunner.PeelBatch"/>) under the project's
    /// max-concurrent-skill-rounds throttle.
    /// </summary>
    Task<StepExecutionResult> RunBatchAsync(
        IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
        LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig,
        PipelineContext context,
        int firstStepIndex,
        CancellationToken cancellationToken);

    /// <summary>
    /// Peel off the leading run of consecutive same-(Name, Round) batchable
    /// commands starting at <paramref name="start"/>. Returns at least one node.
    /// </summary>
    IReadOnlyList<LinkedListNode<PipelineCommand>> PeelBatch(
        LinkedListNode<PipelineCommand> start, int maxConcurrent);
}

/// <summary>
/// Outcome of running a step. <see cref="Result"/> is the CommandResult that
/// drives the executor's continue/abort decision; <see cref="AdvanceTo"/> is
/// always null today (kept for future iterator-driven advance semantics).
/// </summary>
public sealed record StepExecutionResult(
    CommandResult Result,
    LinkedListNode<PipelineCommand>? AdvanceTo);
