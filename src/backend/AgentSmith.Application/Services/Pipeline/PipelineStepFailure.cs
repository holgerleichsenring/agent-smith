using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// 2026-09-17-0e79e: what a run does once it has stopped short — run the finalizer tail,
/// then either deliver what verified or report the failure. Split out of PipelineExecutor,
/// which iterates: there is now more than one way for a run to stop short, and both go
/// through exactly this.
/// <para>
/// p0237: a failed step still runs the finalizer tail (WriteRunResult, CommitAndPR, …) so
/// the run records WHY and keeps its work; the reason is classified by TYPE at the catch
/// site, never parsed from text here.
/// </para>
/// <para>
/// p0439: the tail may DELIVER the verified phases as a shortfall — then the run is a done
/// that says what it lacks, and the error path never runs (no failure comment, no failed
/// status, no WIP persist, no MarkFailed).
/// </para>
/// </summary>
public sealed class PipelineStepFailure(
    PipelineFinalizerTail finalizerTail, IPipelineErrorHandler errorHandler)
{
    /// <summary>
    /// The run spent its <paramref name="budget"/> before reaching <paramref name="stoppedAt"/>,
    /// which therefore has NOT run. The composed result names its step the way
    /// PipelineStepRunner names a failing one — StepName, FailedStep and TotalSteps are what
    /// the ticket comment, the failure log line and the WIP commit's trailer all read, and a
    /// result carrying none of them reports "Step:  (0/0)" to the person holding the ticket.
    /// </summary>
    public Task<CommandResult> ReportExhaustedAsync(
        LinkedListNode<PipelineCommand> stoppedAt,
        LinkedList<PipelineCommand> commands,
        IReadOnlyList<PipelineCommand> pipeline,
        ResolvedProject projectConfig,
        PipelineContext context,
        IAsyncPipelineLifecycle lifecycle,
        StepBudget budget,
        int executionCount,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stoppedAt);
        ArgumentNullException.ThrowIfNull(commands);
        return ReportAsync(
            stoppedAt, commands, pipeline, projectConfig, context, lifecycle,
            CommandResult.Fail($"Pipeline exceeded its {budget}. "
                               + "Possible infinite loop in command insertion.") with
            {
                // The step the run never got to, numbered as the runner would have numbered
                // it, against the LIVE list — an insertion loop's denominator is the list it
                // grew, not the preset it started from.
                FailedStep = executionCount + 1,
                TotalSteps = commands.Count,
                StepName = StepLabelComposer.Label(stoppedAt.Value),
            },
            executionCount, ct);
    }

    /// <param name="stoppedAt">The node the run stopped at — the tail runs from after it.</param>
    /// <param name="commands">The live command list, so the tail's steps keep their context.</param>
    /// <param name="pipeline">The commands the run started with, named in the failure report.</param>
    /// <param name="failure">The failed result, or the one the executor composed for a stop
    /// that produced none of its own.</param>
    /// <returns>The shortfall's summary when the tail delivered one, else the failure.</returns>
    public async Task<CommandResult> ReportAsync(
        LinkedListNode<PipelineCommand> stoppedAt,
        LinkedList<PipelineCommand> commands,
        IReadOnlyList<PipelineCommand> pipeline,
        ResolvedProject projectConfig,
        PipelineContext context,
        IAsyncPipelineLifecycle lifecycle,
        CommandResult failure,
        int executionCount,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(failure);
        context.Set(ContextKeys.FailureReason, failure.Message ?? "unknown");
        await finalizerTail.RunAsync(stoppedAt, commands, projectConfig, context, executionCount, ct);
        if (RunShortfall.DeliveredOn(context) is { } shortfall)
            return CommandResult.Ok(shortfall.Summary);
        await errorHandler.HandleStepFailureAsync(
            pipeline.Select(c => c.Name).ToList(), projectConfig, context, lifecycle, failure, ct);
        return failure;
    }
}
