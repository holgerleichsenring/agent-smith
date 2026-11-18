using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Pipeline error policy. Concerns kept here:
///   - log + post the failure comment (FailureTicketComment) to the ticket
///   - best-effort WIP-persist guard (skip for sourceless / read-only pipelines)
///   - lifecycle.MarkFailed() — the operator-visible Done-vs-Failed terminal signal
/// </summary>
public sealed class PipelineErrorHandler(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
    FailureTicketComment failureComment,
    Lifecycle.UnmovedTicketReport unmovedTickets,
    ILogger<PipelineErrorHandler> logger) : IPipelineErrorHandler
{
    public async Task HandleStepFailureAsync(
        IReadOnlyList<string> commandNames,
        ResolvedProject projectConfig,
        PipelineContext context,
        IAsyncPipelineLifecycle lifecycle,
        CommandResult failure,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("Pipeline stopped at step {Step}: {Step} failed - {Message}",
            failure.FailedStep, failure.StepName, failure.Message);
        // p0261: a FAILED run TERMINALIZES the native ticket status (not just a comment). 2026-09-07-f420:
        // the ticket's ONE comment on a failed run also says what the tail kept and where.
        // 2026-11-16-f3fbb: a precondition stop comments and keeps the ticket's status (no unmoved record).
        var comment = failureComment.ForStep(failure, context);
        await (RunEndKinds.IsPrecondition(failure.FailureKind)
            ? PostTicketStatusAsync(projectConfig, context, comment, cancellationToken)
            : FinalizeFailureAsync(projectConfig, context, comment, cancellationToken));

        await TryPersistWorkBranchAsync(commandNames, projectConfig, context, failure, cancellationToken);
        lifecycle.MarkFailed();
    }

    public Task PostWorkingStatusAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken)
        => PostTicketStatusAsync(projectConfig, context,
            Prompts.OwnTicketComment.WorkingStatus, cancellationToken);

    /// <summary>
    /// p0269: a thrown exception aborts the run before any step produces a failure CommandResult; route it
    /// through the SAME terminalization so the ticket leaves trigger_statuses. No error-type sniffing
    /// (cancellations are filtered out by the caller, they carry their own status semantics).
    /// </summary>
    public Task HandleFatalFailureAsync(
        ResolvedProject projectConfig, PipelineContext context,
        Exception exception, CancellationToken cancellationToken)
    {
        logger.LogWarning(exception,
            "Pipeline aborted by a thrown exception before any step failed — terminalizing ticket status");
        return FinalizeFailureAsync(projectConfig, context,
            failureComment.ForFatal(exception), cancellationToken);
    }

    /// <summary>
    /// p0381: a failure dressed as a cancellation (sandbox-vanished, internal
    /// step/LLM timeout) bypasses BOTH terminalization owners — the fatal path
    /// excludes every OCE, the cancel path owns only operator/watchdog intent.
    /// The run was already published 'failed'; give the TICKET the same failure
    /// semantics so it leaves its trigger status and the poller stops re-claiming.
    /// </summary>
    public Task FinalizeFailedTicketAsync(
        ResolvedProject projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
        => FinalizeFailureAsync(projectConfig, context, failureComment.ForMessage(message), cancellationToken);

    /// <summary>
    /// Best-effort persist of the WIP branch when the pipeline fails after producing
    /// local changes. Wrapped in its OWN try/catch so any persist exception can NEVER
    /// overwrite the original failure cause already in <paramref name="originalFailure"/>.
    /// </summary>
    private async Task TryPersistWorkBranchAsync(
        IReadOnlyList<string> commandNames, ResolvedProject projectConfig, PipelineContext context,
        CommandResult originalFailure, CancellationToken cancellationToken)
    {
        try
        {
            // Stamp the failed step name into context for the WIP commit's trailer block.
            context.Set(ContextKeys.FailedStepName, originalFailure.StepName);

            // Skip persist for source-less / discussion-style runs.
            if (!context.TryGet<AgentSmith.Domain.Entities.Repository>(ContextKeys.Repository, out _))
                return;

            // Skip persist for read-only pipelines (security-scan, api-security-scan, …).
            // The workdir of a review holds scan artifacts (ZAP reports, findings JSON, …)
            // and the tree it was reading, none of which belongs in a WIP commit.
            if (!WorkBranchPersistPolicy.IntendedToChangeCode(commandNames))
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

    private async Task PostTicketStatusAsync(
        ResolvedProject projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
    {
        try
        {
            // 2026-10-26-bdd0b: a spec derivation carries the ticket id and leaves the ticket alone.
            if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null
                || PipelinePresets.LeavesTicketAlone(context))
                return;

            var ticketProvider = ticketFactory.Create(projectConfig.Tracker);
            await ticketProvider.UpdateStatusAsync(ticketId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post status update to ticket");
        }
    }

    /// <summary>
    /// p0261: terminalize a FAILED run — comment AND move the native status to failed_status in one step.
    /// 2026-11-17-166aa: failed_status only, never done; without one it comments only. 2026-11-18-d6d2a: the
    /// status comes from the project (TicketParkStatuses, the gate's source), not the request context, whose
    /// values the Redis job queue hands back as JsonElement. Best-effort: a write failure is logged and said.
    /// </summary>
    private async Task FinalizeFailureAsync(
        ResolvedProject projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
    {
        // 2026-10-26-bdd0b: a spec derivation carries the ticket id and leaves the ticket alone.
        if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null
            || PipelinePresets.LeavesTicketAlone(context))
            return;
        var failedStatus = Tickets.TicketParkStatuses.Of(projectConfig).Failed;
        var unmoved = failedStatus is null ? null : await MoveToFailedAsync(projectConfig, ticketId, message, failedStatus, cancellationToken);
        if (failedStatus is null || unmoved is not null)
            await PostTicketStatusAsync(projectConfig, context, failedStatus is null
                ? message : Lifecycle.UnmovedStatusNote.Append(message, "failed_status", failedStatus, unmoved!), cancellationToken);
    }

    // 2026-11-18-d6d2a: why the status did not move when the ticket must hear it — a move the tracker rejected
    // (AzDO loses the comment with it) or a write that threw; null when it moved or the comment already landed.
    private async Task<string?> MoveToFailedAsync(ResolvedProject projectConfig, TicketId ticketId,
        string message, string failedStatus, CancellationToken cancellationToken)
    {
        try
        {
            var finalize = await ticketFactory.Create(projectConfig.Tracker)
                .FinalizeAsync(ticketId, message, failedStatus, cancellationToken);
            await unmovedTickets.RecordAsync(
                projectConfig.Name, projectConfig.Tracker.Name, ticketId, finalize, cancellationToken);
            return finalize.Outcome == TicketFinalizeOutcome.TrackerRejectedTheStatus ? finalize.Outcome.ToString() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to finalize ticket to failed_status — run/PR unaffected");
            return ex.GetType().Name;
        }
    }
}
