using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0330: terminalizes the TRACKER ticket of a cancelled run. The run row alone
/// is not durable enough — the ticket still sits in trigger_statuses and the next
/// poll would re-claim it as a fresh run. Uses the same failed_status fallback
/// chain as the pipeline failure path (trigger.failed_status, then done_status,
/// then the provider default when null). Fail-soft by design: a tracker error is
/// logged and swallowed — it must never block a cancel.
/// </summary>
public sealed class CancelledTicketFinalizer(
    ITicketProviderFactory ticketFactory,
    IConfigurationLoader configLoader,
    IActiveRunLease activeRunLease,
    ServerContext serverContext,
    ILogger<CancelledTicketFinalizer> logger)
{
    public async Task FinalizeAsync(
        string project, string ticketId, string runId, string comment, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(ticketId)) return;
            // p0355: ownership guard. When this run was reaped and a NEWER run has
            // already reclaimed the ticket (its lease now names a different run id),
            // finalizing here would drag the ticket the new run is actively working
            // to a terminal status (observed: a reaped run set the ticket Resolved
            // while the new run set it InProgress). Skip — the active run owns it.
            if (await ReclaimedByAnotherRunAsync(project, ticketId, runId, cancellationToken))
            {
                logger.LogInformation(
                    "Skipping ticket finalize for {Project}/#{Ticket}: reclaimed by a newer run (this run {RunId} was superseded)",
                    project, ticketId, runId);
                return;
            }
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            if (!config.Projects.TryGetValue(project, out var resolved)) return;

            var trigger = TriggerSelectionHelper.ByTrackerType(resolved, resolved.Tracker.Type);
            var status = !string.IsNullOrEmpty(trigger?.FailedStatus)
                ? trigger!.FailedStatus
                : trigger?.DoneStatus;
            await ticketFactory.Create(resolved.Tracker)
                .FinalizeAsync(new TicketId(ticketId), comment, status, cancellationToken);
            logger.LogInformation(
                "Cancelled run's ticket {Project}/#{Ticket} finalized to '{Status}'",
                project, ticketId, status ?? "(provider default)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to finalize cancelled run's ticket {Project}/#{Ticket} — cancel unaffected",
                project, ticketId);
        }
    }

    // True when the ticket's active lease names a run OTHER than this one — a newer
    // run reclaimed it. An empty runId (no ownership to defend) or a read error is
    // NOT a reclaim: the finalize proceeds (fail-open) so a lease-read glitch never
    // leaves a cancelled ticket stuck in its trigger status.
    private async Task<bool> ReclaimedByAnotherRunAsync(
        string project, string ticketId, string runId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(runId)) return false;
        try
        {
            var lease = await activeRunLease.GetByTicketAsync(project, new TicketId(ticketId), ct);
            return lease?.RunId is { Length: > 0 } owner
                && !string.Equals(owner, runId, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex,
                "Ownership check failed for {Project}/#{Ticket} — finalizing anyway", project, ticketId);
            return false;
        }
    }
}
