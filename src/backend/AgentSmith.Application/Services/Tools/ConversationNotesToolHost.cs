using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4ck: read_notes and write_notes — the design conversation's working file, which the master keeps across
/// turns as Claude Code keeps notes in a file across a session. The next turn's prompt carries it whole. Built from
/// the store and conversation a turn was seeded with; a turn seeded with neither gets no tools.
/// </summary>
public sealed class ConversationNotesToolHost(IConversationNotes notes, string conversationId) : IToolHost
{
    public const string ReadName = "read_notes";
    public const string WriteName = "write_notes";

    /// <summary>The host for a design turn's pipeline, or null when it carries no store or conversation.</summary>
    public static ConversationNotesToolHost? For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<IConversationNotes>(ContextKeys.SpecDialogNotesStore, out var store) && store is not null
            && pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var conversation) && !string.IsNullOrEmpty(conversation)
            ? new ConversationNotesToolHost(store, conversation)
            : null;
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode) =>
        [AIFunctionFactory.Create(ReadNotes, name: ReadName), AIFunctionFactory.Create(WriteNotes, name: WriteName)];

    [Description("Reads this conversation's notes — the working file you keep across turns. Each turn's prompt "
        + "already carries them as the turn began; read them again after you wrote them.")]
    public async Task<string> ReadNotes(CancellationToken ct = default) =>
        await notes.ReadAsync(conversationId, ct) is { Length: > 0 } text ? text : "No notes yet.";

    [Description("Replaces this conversation's notes with the whole text you pass: what the material is, how to run "
        + "it, what each area says and which files a fact rests on, what is settled and what is still open. The next "
        + "turn's prompt carries them whole, and a run that carries an upload of this conversation shows them.")]
    public async Task<string> WriteNotes(
        [Description("The whole notes file, replacing the one before.")] string text,
        CancellationToken ct = default)
    {
        await notes.WriteAsync(conversationId, text ?? string.Empty, ct);
        return $"Stored {text?.Length ?? 0} characters.";
    }
}
