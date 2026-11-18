using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// 2026-11-12-a1fa: builds the design master's tool surface that hands on what the operator wrote mid-turn. Only the
/// master's own functions are wrapped — sub-agents are built elsewhere and never drain — and only when the turn runner
/// seeded the turn's messages. The ticket steward's turn is told it leads and gets the surface unchanged.
/// <para>2026-11-17-4860b: a code run working a spec of a ticket gets the same surface over the messages its ticket's
/// conversation addressed to it (<see cref="TicketRunMessages"/>), where the server registered the conversations.</para>
/// </summary>
public sealed class MidTurnMessageTools(ITicketConversationMessages? conversations = null)
{
    /// <param name="notices">2026-11-18-c8e2a: the master's sub-agent team notices — handed on beside the operator's
    /// messages, and alone where there are none (a scan master, a ticketless run) or the steward keeps them.</param>
    public IList<AITool> Delivering(IList<AITool> tools, AgenticMasterContext context, IMidTurnMessages? notices = null)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(context);
        var operatorMessages = Seeded(context) ?? OfTicket(context.Pipeline);
        operatorMessages?.Lead(context.IsTicketSteward);
        var messages = CombinedMidTurnMessages.Of(context.IsTicketSteward ? null : operatorMessages, notices);
        if (messages is null) return tools;
        return [.. tools.Select(tool => tool is AIFunction f ? new MidTurnMessageAIFunction(f, messages) : tool)];
    }

    private static IMidTurnMessages? Seeded(AgenticMasterContext context) =>
        context.Pipeline.TryGet<IMidTurnMessages>(ContextKeys.SpecDialogOperatorMessages, out var seeded) ? seeded : null;

    // A spec of a ticket, worked by a run with an id, in a named project.
    private TicketRunMessages? OfTicket(PipelineContext pipeline) =>
        conversations is not null && pipeline.Has(ContextKeys.CurrentSpec)
        && pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null
        && pipeline.TryGet<string>(ContextKeys.RunId, out var run) && !string.IsNullOrEmpty(run)
        && pipeline.TryGet<string>(ContextKeys.ProjectName, out var project) && !string.IsNullOrEmpty(project)
            ? new TicketRunMessages(conversations, pipeline, project, ticket.Id.Value, run)
            : null;
}
