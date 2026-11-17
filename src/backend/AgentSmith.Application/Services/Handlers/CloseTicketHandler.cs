using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-e061a: the step of a 'close' run — closes what the request names and puts what it did into the outcome
/// slot the server reads back. A missing seed fails naming the key; the close itself never fails the step, because
/// each host's outcome is reported, not thrown.
/// </summary>
public sealed class CloseTicketHandler(TicketCloseActs acts) : ICommandHandler<CloseTicketContext>
{
    public async Task<CommandResult> ExecuteAsync(CloseTicketContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<CloseTicketRequest>(ContextKeys.CloseTicketRequest, out var request) || request is null)
            return CommandResult.Fail("A 'close' run closes what the server asks, and this run carries no request under "
                + $"ContextKeys.{nameof(ContextKeys.CloseTicketRequest)}. Nothing was closed.");
        if (!pipeline.TryGet<ActSlot<TicketCloseOutcome>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("A 'close' run needs the outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.ActOutcome)}; nothing was closed, because nobody could read what was.");
        var outcome = await acts.CloseAsync(context.Project, request);
        slot.Set(outcome);
        return CommandResult.Ok($"Ticket {request.TicketId}: {outcome.PullRequests.Count(p => p is { Closed: true, Reason: null })} "
            + $"pull request(s) closed, tracker {(outcome.Tracker is null ? "not asked" : outcome.Tracker.Closed ? "closed" : "not closed")}"
            + (outcome.Mark is null ? string.Empty : ", mark stored"));
    }
}
