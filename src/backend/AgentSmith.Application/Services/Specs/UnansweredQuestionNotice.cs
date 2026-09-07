using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Tells the ticket which reading the run proceeded on after its question went
/// unanswered. A plain comment — the ticket keeps its status, the run continues — so
/// the author who ignored the question still sees which door the run went through,
/// and can still correct it: a comment plus a re-trigger re-cuts the unstarted tail.
/// The reading is named from the question that was asked, not from this run's reply.
/// </summary>
public sealed class UnansweredQuestionNotice(
    ITicketProviderFactory ticketFactory,
    ILogger<UnansweredQuestionNotice> logger)
{
    public async Task PostAsync(
        PipelineContext pipeline, TrackerConnection? tracker, UnansweredQuestion unanswered,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(unanswered);
        if (tracker is null) return;
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return;
        // An inline ticket exists only on this run — there is nothing to comment on.
        if (pipeline.Has(ContextKeys.InlineTicket)) return;

        try
        {
            await ticketFactory.Create(tracker).UpdateStatusAsync(ticket.Id, Build(unanswered), ct);
            logger.LogInformation(
                "Told ticket {Ticket} the run proceeds on reading {Label}",
                ticket.Id.Value, unanswered.TakenLabel);
        }
        catch (Exception ex)
        {
            // The cut is on the branch and in the pull request either way; a tracker that
            // refuses a comment must not end a run that is otherwise fine.
            logger.LogWarning(ex, "Could not post the proceed notice to ticket {Ticket}", ticket.Id.Value);
        }
    }

    private static string Build(UnansweredQuestion unanswered) =>
        $"## Agent Smith — proceeding on reading {unanswered.TakenLabel}\n\n"
        + "The question below was not answered and the ticket was re-triggered, so the run "
        + "proceeds on the reading it said it would take:\n\n"
        + $"> {unanswered.TakenLabel} {unanswered.TakenReading}\n\n"
        + "If that is the wrong reading, comment with the one you mean and move the ticket back "
        + "to a trigger status; the next run re-cuts the phases that have not started.";
}
