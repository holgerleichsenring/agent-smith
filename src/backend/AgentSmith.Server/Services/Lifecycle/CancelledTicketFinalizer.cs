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
    ServerContext serverContext,
    ILogger<CancelledTicketFinalizer> logger)
{
    public async Task FinalizeAsync(
        string project, string ticketId, string comment, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(ticketId)) return;
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
}
