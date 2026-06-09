using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Pipeline error policy. Concerns kept here:
///   - log + post HTML-formatted failure comment to the ticket
///   - best-effort WIP-persist guard (skip for sourceless / read-only pipelines)
///   - lifecycle.MarkFailed() — the operator-visible Done-vs-Failed terminal signal
/// </summary>
public sealed class PipelineErrorHandler(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
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
        var executionCount = failure.FailedStep;
        var total = failure.TotalSteps;
        var label = failure.StepName;

        logger.LogWarning("Pipeline stopped at step {Step}: {Step} failed - {Message}",
            executionCount, label, failure.Message);
        // HTML-formatted: AzDO System.History accepts HTML; GitHub/GitLab markdown comments
        // render inline HTML; only Jira's ADF flattens it to plain text (acceptable fallback).
        var safeMessage = System.Net.WebUtility.HtmlEncode(failure.Message ?? "");
        // p0261: a FAILED run TERMINALIZES the native ticket status (not just a comment),
        // so the ticket no longer reads as New/Active — the same one-step comment+status
        // move success uses. The "working" status (PostWorkingStatusAsync) deliberately
        // stays a comment-only UpdateStatus; only this terminal failure moves the status.
        await FinalizeFailureAsync(projectConfig, context,
            $"<b>Agent Smith — Failed</b><br/>" +
            $"<b>Step:</b> {System.Net.WebUtility.HtmlEncode(label)} ({executionCount}/{total})<br/>" +
            $"<b>Error:</b> {safeMessage}", cancellationToken);

        await TryPersistWorkBranchAsync(commandNames, projectConfig, context, failure, cancellationToken);
        lifecycle.MarkFailed();
    }

    public Task PostWorkingStatusAsync(
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken)
        => PostTicketStatusAsync(projectConfig, context,
            "Agent Smith is working on this issue...", cancellationToken);

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
            // Without a code-modifying handler in the pipeline, the workdir contains scan
            // artifacts (ZAP reports, findings JSON, …) that should NOT be staged into a
            // WIP branch. Code-modifying handlers are AgenticExecute / GenerateTests /
            // GenerateDocs — pipelines without any of those produce no source mutation.
            if (!ContainsCodeModifyingHandler(commandNames))
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

    private static bool ContainsCodeModifyingHandler(IReadOnlyList<string> commandNames) =>
        commandNames.Any(n => n == CommandNames.AgenticExecute
                           || n == CommandNames.AgenticMaster // p0202c: the post-p0179b coding handler
                           || n == CommandNames.GenerateTests
                           || n == CommandNames.GenerateDocs);

    private async Task PostTicketStatusAsync(
        ResolvedProject projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
    {
        try
        {
            if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
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
    /// p0261: terminalize a FAILED run — post the failure comment AND move the native
    /// ticket status to failed_status in one provider-native step (mirrors the success
    /// path's FinalizeAsync(doneStatus)). Resolves failed_status from the pipeline
    /// context, falling back to done_status, then to the provider's own default when
    /// FinalizeAsync is handed null — so the status ALWAYS changes and the ticket never
    /// stays in a trigger status. Best-effort: a write failure (e.g. a deleted ticket)
    /// is logged, never rethrown, so it can't invalidate an already-produced PR.
    /// </summary>
    private async Task FinalizeFailureAsync(
        ResolvedProject projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
    {
        try
        {
            if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
                return;

            if (!context.TryGet<string>(ContextKeys.FailedStatus, out var failedStatus)
                || string.IsNullOrEmpty(failedStatus))
                context.TryGet<string>(ContextKeys.DoneStatus, out failedStatus);

            var ticketProvider = ticketFactory.Create(projectConfig.Tracker);
            await ticketProvider.FinalizeAsync(ticketId, message, failedStatus, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to finalize ticket to failed_status — run/PR unaffected");
        }
    }
}
