using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
/// <para>The hand-back is posted on the ticket's pull requests too, marked as ours — a reviewer who
/// works there answers there, and a comment command answers the hand-back with a rework.</para>
/// <para>2026-10-13-9a59g: the posted words are published as <see cref="SpecHandedBackEvent"/> — the
/// comment is otherwise kept only by the tracker — and nothing is published outside a run.</para>
/// </summary>
public sealed class SpecHandbackPark(
    ITicketProviderFactory ticketFactory,
    Lifecycle.UnmovedTicketReport unmovedTickets,
    AgentSmith.Contracts.Reviews.IHandbackPrNotice prNotice,
    IEventPublisher events,
    IRunContextAccessor runContext,
    ILogger<SpecHandbackPark> logger)
{
    public async Task ParkAsync(
        SpecHandbackContext context, string project, SpecHandback handback, string status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var waitingLine = TicketMention.WaitingLine(context.Tracker!.Type, context.Ticket);
        var comment = SpecHandbackComment.Build(handback, SpecPullRequests.Of(context.Pipeline), waitingLine);
        var finalize = await ticketFactory.Create(context.Tracker!).FinalizeAsync(
            context.Ticket!.Id, comment, status, cancellationToken);
        await prNotice.PostAsync(context.Pipeline, comment, cancellationToken);
        await PublishAsync(comment, handback.Quote, cancellationToken);
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

    private async Task PublishAsync(string comment, string? quote, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId)) return;
        try
        {
            await events.PublishAsync(
                new SpecHandedBackEvent(runId, DateTimeOffset.UtcNow, comment, quote), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "The hand-back of run {RunId} was posted but not published", runId);
        }
    }
}
