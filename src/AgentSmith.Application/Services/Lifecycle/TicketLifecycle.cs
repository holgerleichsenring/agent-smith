using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Post-PR ticket finalization shared by CommitAndPRHandler and InitCommitHandler:
/// transition to a configured done-status (or close as a fallback) and post a
/// PR-link summary comment. Operates on TicketId — does not require a fetched
/// Ticket — so handlers that only have the id (init-project's label-triggered
/// path) can call it without an extra FetchTicket step.
/// </summary>
public static class TicketLifecycle
{
    public static async Task FinalizeAsync(
        ITicketProviderFactory ticketFactory,
        TrackerConnection ticketConfig,
        TicketId ticketId,
        string? doneStatus,
        string summary,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = ticketFactory.Create(ticketConfig);

            if (!string.IsNullOrWhiteSpace(doneStatus))
            {
                await provider.UpdateStatusAsync(ticketId, summary, cancellationToken);
                await provider.TransitionToAsync(ticketId, doneStatus, cancellationToken);
                logger.LogInformation(
                    "Ticket {Ticket} transitioned to '{DoneStatus}' with summary",
                    ticketId, doneStatus);
            }
            else
            {
                await provider.CloseTicketAsync(ticketId, summary, cancellationToken);
                logger.LogInformation("Ticket {Ticket} closed with summary", ticketId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to finalize ticket {Ticket}, PR was still created", ticketId);
        }
    }
}
