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
/// <remarks>
/// Delegates to <see cref="ITicketProvider.FinalizeAsync"/> so the provider can
/// pick the atomic-vs-sequential shape that fits its backend. AzDO collapses
/// both writes into one PATCH to avoid the TF26071 (System.Rev) race that bit
/// production when comment + state landed as two PATCHes; GitHub/GitLab/Jira
/// stay on two sequential calls (no rev guard exists there).
/// </remarks>
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
            await provider.FinalizeAsync(ticketId, summary, doneStatus, cancellationToken);
            logger.LogInformation(
                "Ticket {Ticket} finalized (status='{Status}')",
                ticketId, doneStatus ?? "<close>");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to finalize ticket {Ticket}, PR was still created", ticketId);
        }
    }
}
