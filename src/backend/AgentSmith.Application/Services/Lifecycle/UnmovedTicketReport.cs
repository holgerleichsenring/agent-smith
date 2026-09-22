using AgentSmith.Contracts.Models;
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
    /// <summary>
    /// 2026-09-22-7c41b: the project and the tracker by NAME — the two values this ever read.
    /// A resolved project is not in hand at every site that finalizes a ticket: a park holds
    /// its tracker connection and the run's project name and nothing else.
    /// </summary>
    public Task RecordAsync(
        string project, string tracker, TicketId ticketId,
        TicketFinalizeResult finalize, CancellationToken cancellationToken)
    {
        if (finalize.StatusMoved)
            return store.ClearAsync(project, ticketId.Value, cancellationToken);

        logger.LogWarning(
            "Ticket {Ticket} did not move to '{Status}' ({Outcome}) — it is not claimed again "
            + "until the tracker's or the project's configuration changes",
            ticketId.Value, finalize.RequestedStatus, finalize.Outcome);
        return store.RecordAsync(
            new UnmovedTicketFact(
                project, ticketId.Value, tracker,
                finalize.RequestedStatus ?? string.Empty, finalize.Outcome),
            cancellationToken);
    }

    /// <summary>
    /// 2026-09-22-7c41b: the same fact, recorded from a PARK. A park that the tracker refused
    /// left the ticket in a trigger status with a question on it, so the next poll would claim
    /// it and start a second run against a question nobody has answered yet.
    /// <para>
    /// The rule is an outcome SET, not a category. A tracker that looked at the status and
    /// refused it, and a tracker whose workflow offers no transition to it, will both answer
    /// the same way next time — that is the standing fact. A tracker whose issue status is its
    /// open-or-closed state can express no clarification status at all, and a caller that
    /// requested none asked nothing: neither says anything about this ticket, so neither
    /// records and neither clears.
    /// </para>
    /// </summary>
    public Task RecordParkAsync(
        string project, string tracker, TicketId ticketId,
        TicketFinalizeResult finalize, CancellationToken cancellationToken) => finalize.Outcome switch
        {
            // A park that landed clears a standing fact, so a corrected configuration stops
            // refusing the ticket the first time a park succeeds.
            TicketFinalizeOutcome.Moved or
            TicketFinalizeOutcome.TrackerRejectedTheStatus or
            TicketFinalizeOutcome.NoTransitionToTheStatus =>
                RecordAsync(project, tracker, ticketId, finalize, cancellationToken),
            _ => Task.CompletedTask,
        };
}
