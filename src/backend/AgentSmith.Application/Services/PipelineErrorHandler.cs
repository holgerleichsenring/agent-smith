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
        await PostTicketStatusAsync(projectConfig, context,
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
}
