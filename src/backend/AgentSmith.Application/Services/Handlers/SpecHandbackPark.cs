using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-22-7c41b: the hand-back's park. <see cref="SpecHandbackHandler"/> decides WHETHER
/// to park and in which status; this performs it and reports what the tracker did with the
/// ticket, which is not the same thing as what was asked of it.
/// <para>
/// A status move the tracker refused leaves the ticket in a trigger status with a hand-back
/// comment on it, so the next poll would claim it and run it again. The standing fact holds it
/// instead, naming the parking status that is wrong. This park writes no checkpoint, so the
/// fact closes its one re-entry deliberately: the configuration genuinely is wrong, and the
/// refusal an operator reads names the field that fixes it.
/// </para>
/// </summary>
public sealed class SpecHandbackPark(
    ITicketProviderFactory ticketFactory,
    Lifecycle.UnmovedTicketReport unmovedTickets,
    ILogger<SpecHandbackPark> logger)
{
    public async Task ParkAsync(
        SpecHandbackContext context, string project, SpecHandback handback, string status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var prUrl = context.Pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var url)
            ? url : null;
        var waitingLine = TicketMention.WaitingLine(context.Tracker!.Type, context.Ticket);
        var finalize = await ticketFactory.Create(context.Tracker!).FinalizeAsync(
            context.Ticket!.Id, SpecHandbackComment.Build(handback, prUrl, waitingLine), status,
            cancellationToken);
        // The awaiting-answer flag short-circuits the rest of the run for BOTH classes:
        // there is nothing to build either way. What differs is how the ticket comes back —
        // an answered question re-triggers, a verdict waits for a Retry.
        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        await unmovedTickets.RecordParkAsync(
            project, context.Tracker!.Name, context.Ticket.Id, finalize, cancellationToken);
        logger.LogInformation(
            "Ticket {Ticket} handed back ({Case}){Parked}",
            context.Ticket.Id.Value, handback.Case,
            finalize.StatusMoved
                ? $" — parked in {status}"
                : " — the tracker did not move it, so it is held from the next poll");
    }
}
