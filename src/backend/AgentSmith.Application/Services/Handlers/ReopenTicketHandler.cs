using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-e061a: the step of a 'reopen' run — clears the ticket's close mark and tells the open pages, putting
/// whether there was a mark into the outcome slot. Reopen clears the mark only: the tracker ticket stays as it is.
/// </summary>
public sealed class ReopenTicketHandler(
    ITicketClosedMarkStore marks, ITicketActivityNudge activity, ILogger<ReopenTicketHandler> logger)
    : ICommandHandler<ReopenTicketContext>
{
    public async Task<CommandResult> ExecuteAsync(ReopenTicketContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<ReopenTicketRequest>(ContextKeys.ReopenTicketRequest, out var request) || request is null
            || !pipeline.TryGet<ActSlot<bool>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("A 'reopen' run needs the request and outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.ReopenTicketRequest)} and ContextKeys.{nameof(ContextKeys.ActOutcome)}. Nothing was reopened.");
        var project = context.Project;
        var cleared = await marks.ClearAsync(project.Tracker.Name, request.Key, CancellationToken.None);
        slot.Set(cleared);
        if (!cleared) return CommandResult.Ok($"Ticket {request.TicketId} carried no close mark; nothing to reopen");
        logger.LogInformation("Ticket {Project}/{Ticket} was reopened", project.Name, request.TicketId);
        try { await activity.ChangedAsync(project.Name, request.Key, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Ticket {Project}/{Ticket} is reopened but a nudge was lost", project.Name, request.TicketId); }
        return CommandResult.Ok($"Ticket {request.TicketId} reopened");
    }
}
