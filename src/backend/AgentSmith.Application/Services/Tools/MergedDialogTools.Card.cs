using System.ComponentModel;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;
using static AgentSmith.Application.Services.Tools.MergedTools;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-18-9c1cd: what the dialog master states for the conversation's card — its name, its picture and its questions.</summary>
public static partial class MergedDialogTools
{
    /// <summary>state_work_picture with state_open_questions folded in.</summary>
    public static AIFunction StateWorkPicture(AIFunction? picture, AIFunction? questions) =>
        AIFunctionFactory.Create(
            async ([Description(WorkPictureToolHost.KindDescription)] string? kind = null,
                   [Description("One line of plain prose: where what is wanted is described (an upload, the ticket, the conversation) and what the repositories already provide.")] string? truth = null,
                   [Description("one-spec, series or plan.")] string? size = null,
                   [Description(WorkPictureToolHost.EffortDescription)] string? effort = null,
                   [Description("The questions only the operator can answer, for the draft's card: each {question, proposal, specIds}. Write the question and its proposal in the language the operator writes in; the proposal is a plain sentence with its reason and no leading label. The operator takes your proposal or answers differently; a question still open when they approve is filed as decided on your proposal. A later call replaces the list.")] OpenQuestion[]? open_questions = null,
                   CancellationToken ct = default) =>
            {
                var states = !string.IsNullOrWhiteSpace(kind) || !string.IsNullOrWhiteSpace(truth) || !string.IsNullOrWhiteSpace(size);
                if (!states && open_questions is null)
                    return "Error: pass the picture (kind, truth, size) or open_questions, or both.";
                var answers = new List<string>();
                if (states)
                    answers.Add(Text(await Call(picture, "Error: this turn keeps no work picture.", ct,
                        ("kind", kind ?? string.Empty), ("truth", truth ?? string.Empty), ("size", size ?? string.Empty), ("effort", effort))));
                if (open_questions is not null)
                    answers.Add(Text(await Call(questions, "Error: this turn keeps no open questions.", ct, ("questions", open_questions))));
                return string.Join("\n", answers);
            },
            WorkPictureToolHost.Name,
            "Records this conversation's work picture — the kind of undertaking, where its truth lives, its size and the "
            + "effort its review may spend — and/or the open questions that ride the draft. Call it again to correct; the "
            + "last call replaces each. It is not a source read.");

    /// <summary>name_conversation, offered on every turn; a named conversation keeps its name.</summary>
    public static AIFunction NameConversation(AIFunction? name) =>
        AIFunctionFactory.Create(
            async ([Description("One line of plain prose, no markup, at most 120 characters.")] string subject,
                   CancellationToken ct = default) =>
                await Call(name, "Not stored: this conversation already has a name, and it keeps it. Write your reply.", ct, ("subject", subject)),
            NameConversationToolHost.Name,
            "Names a conversation that has no name yet, for the heading above it: one line naming what it is about, in "
            + "the language you answer in. Call it before writing the reply. A conversation that has a name keeps it.");

    private static string Text(object? result) => result switch
    {
        null => string.Empty,
        string s => s,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } e => e.GetString() ?? string.Empty,
        var other => other.ToString() ?? string.Empty,
    };
}
