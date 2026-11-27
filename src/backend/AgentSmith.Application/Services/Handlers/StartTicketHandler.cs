using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-e061b: the step of a 'start' run — makes an existing ticket claimable the way the poller would resolve it
/// (<see cref="FiledWorkMove.StartExistingAsync"/>: its routing tag, then a move into a trigger status) and puts the
/// start into the outcome slot. It runs on no token: once the tag lands, the move belongs with it. A not-started ticket
/// is an answer, not a failure; only missing seeds fail the step.
/// </summary>
public sealed class StartTicketHandler(FiledWorkMove move, ITicketProviderFactory trackers) : ICommandHandler<StartTicketContext>
{
    public async Task<CommandResult> ExecuteAsync(StartTicketContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<StartTicketRequest>(ContextKeys.StartTicketRequest, out var request) || request is null
            || !pipeline.TryGet<ActSlot<FiledWorkStart>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("A 'start' run needs the request and outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.StartTicketRequest)} and ContextKeys.{nameof(ContextKeys.ActOutcome)}. Nothing was started.");
        var project = context.Project;
        var started = await move.StartExistingAsync(
            trackers.Create(project.Tracker), project, request.TicketId, request.MayStartRuns, CancellationToken.None);
        slot.Set(started);
        return CommandResult.Ok($"Ticket {request.TicketId}: {started.State} — {started.Reason}");
    }
}
