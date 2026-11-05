using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-04-96a0a: the line under the reply contract that asks for the conversation's name. Rendered only on a turn
/// seeded as nameable, which is the turn name_conversation is offered on, so it never asks for a tool the turn lacks.
/// </summary>
public static class ConversationNameNote
{
    public const string Text =
        "This conversation has no name yet. Before writing the reply, call " + NameConversationToolHost.Name
        + " once, in the language of this reply. It is not a source read: call it even when the answer needs no tool.";

    public static string Render(PipelineContext pipeline) =>
        pipeline.TryGet<bool>(ContextKeys.SpecDialogNameable, out var nameable) && nameable ? Text : string.Empty;
}
