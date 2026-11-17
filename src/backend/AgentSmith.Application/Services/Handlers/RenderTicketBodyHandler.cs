using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-dd05b: the step of a 'ticket-body' run — renders the ticket's body from the specs on its branch and puts
/// how it ended into the outcome slot the server reads back. A body that could not be rewritten fails the step.
/// </summary>
public sealed class RenderTicketBodyHandler(TicketBodyRender render) : ICommandHandler<RenderTicketBodyContext>
{
    public async Task<CommandResult> ExecuteAsync(RenderTicketBodyContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<TicketBodyRequest>(ContextKeys.TicketBodyRequest, out var request) || request is null
            || !pipeline.TryGet<ActSlot<TicketBodyOutcome>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("A 'ticket-body' run needs the request and outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.TicketBodyRequest)} and ContextKeys.{nameof(ContextKeys.ActOutcome)}. Nothing was rewritten.");
        var outcome = await render.RenderAsync(context.Project, request, CancellationToken.None);
        slot.Set(outcome);
        return outcome.Rewritten ? CommandResult.Ok(outcome.Message) : CommandResult.Fail(outcome.Message);
    }
}
