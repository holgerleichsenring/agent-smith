using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// 2026-11-12-a1fa: builds the design master's tool surface that hands on what the operator wrote mid-turn. Only the
/// master's own functions are wrapped — sub-agents are built elsewhere and never drain — and only when the turn runner
/// seeded the turn's messages. The ticket steward's turn is told it leads and gets the surface unchanged.
/// </summary>
public sealed class MidTurnMessageTools
{
    public IList<AITool> Delivering(IList<AITool> tools, AgenticMasterContext context)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<IMidTurnMessages>(ContextKeys.SpecDialogOperatorMessages, out var messages)
            || messages is null)
            return tools;
        messages.Lead(context.IsTicketSteward);
        if (context.IsTicketSteward) return tools;
        return [.. tools.Select(tool => tool is AIFunction f ? new MidTurnMessageAIFunction(f, messages) : tool)];
    }
}
