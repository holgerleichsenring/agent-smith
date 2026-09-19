using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-09-18-c1a7: turns a finalize into the fact the claim service reads. A status that did
/// not move leaves a record — the ticket is still in a status the poller claims and it will
/// fail the same way until an operator changes the configured value, so the fact, not another
/// attempt, is what the run owes the next poll cycle. A status that DID move clears one, so a
/// record never outlives the configuration that caused it.
/// </summary>
public sealed class UnmovedTicketReport(
    IUnmovedTicketStore store, ILogger<UnmovedTicketReport> logger)
{
    public Task RecordAsync(
        ResolvedProject project, TicketId ticketId,
        TicketFinalizeResult finalize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (finalize.StatusMoved)
            return store.ClearAsync(project.Name, ticketId.Value, cancellationToken);

        logger.LogWarning(
            "Ticket {Ticket} did not move to '{Status}' ({Outcome}) — it is not claimed again "
            + "until the tracker's or the project's configuration changes",
            ticketId.Value, finalize.RequestedStatus, finalize.Outcome);
        return store.RecordAsync(
            new UnmovedTicketFact(
                project.Name, ticketId.Value, project.Tracker.Name,
                finalize.RequestedStatus ?? string.Empty, finalize.Outcome),
            cancellationToken);
    }
}
