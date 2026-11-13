using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The limits a DIALOG turn runs under, applied to a request shaped for the coding surface: its own iteration
/// ceiling, the agent's compaction against the dialog window, the capture of what it forwards, and the thread its
/// master's previous turn ended with as the messages it continues. Raising the ceiling is safe only because the
/// window and compaction travel with it; replaying the thread is safe only because the turn can fold it.
/// </summary>
public static class DialogSurfaceLimits
{
    public static AgenticLoopRequest Apply(AgenticLoopRequest request, AgenticMasterContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var config = context.AgentConfig;
        var slot = context.Pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var s) ? s : null;
        slot?.Thread.Lead(context.IsTicketSteward); // 2026-11-12-92e9a: travels with what the sink is offered
        return request with
        {
            MaxIterations = config.DialogMasterLoopIterations,
            Compaction = config.Compaction,
            ContextWindowTokensOverride = config.DialogContextWindowTokens,
            ThreadCapture = slot?.Thread,
            PriorMessages = DialogThreadSeed.For(context.Pipeline, context.IsTicketSteward)?.Messages,
        };
    }
}
