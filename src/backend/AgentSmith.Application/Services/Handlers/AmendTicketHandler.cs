using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-e061c: the step of an 'amend' run — rewrites the description, records the approved series and writes it
/// to the ticket branch, putting what the thread is told into the outcome slot. A write that throws is told, not
/// thrown: the conversation asked for it and hears how it ended; the step then fails, so nothing after it runs.
/// </summary>
public sealed class AmendTicketHandler(TicketAmendmentWrite write, ILogger<AmendTicketHandler> logger) : ICommandHandler<AmendTicketContext>
{
    public async Task<CommandResult> ExecuteAsync(AmendTicketContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<AmendTicketRequest>(ContextKeys.AmendTicketRequest, out var request) || request is null
            || !pipeline.TryGet<ActSlot<TicketAmendmentResult>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("An 'amend' run needs the request and outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.AmendTicketRequest)} and ContextKeys.{nameof(ContextKeys.ActOutcome)}. Nothing was recorded.");
        try
        {
            slot.Set(new TicketAmendmentResult(await write.WriteAsync(context.Project, request, CancellationToken.None), true));
            return CommandResult.Ok($"Approved series recorded under ticket {request.TicketId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recording the approval under ticket {Ticket} failed", request.TicketId);
            var notice = $"The approval under ticket {request.TicketId} did not complete: {ex.Message}. Check the "
                + "ticket's approved series before triggering it, and approve again.";
            slot.Set(new TicketAmendmentResult(notice, false));
            return CommandResult.Fail(notice);
        }
    }
}
