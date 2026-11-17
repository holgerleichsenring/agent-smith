using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the admin turn's tools as the conversation needs them — each call reported on the turn's activity
/// line, and the administrator's messages sent while it runs handed on with the next result, the design turn's way.
/// </summary>
public sealed class AdminTurnTools(TurnActivityTools activity)
{
    public IList<AITool> For(PipelineContext pipeline, IList<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var reported = activity.Reporting(tools);
        if (!pipeline.TryGet<IMidTurnMessages>(ContextKeys.SpecDialogOperatorMessages, out var messages) || messages is null)
            return reported;
        messages.Lead(stewarded: false);
        return [.. reported.Select(tool => tool is AIFunction f ? new MidTurnMessageAIFunction(f, messages) : tool)];
    }
}
