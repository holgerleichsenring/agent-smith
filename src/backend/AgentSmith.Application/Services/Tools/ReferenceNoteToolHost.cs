using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-02-075dd: note_reference — replaces an upload's one note: what it is, how to run it,
/// what it needs, what was seen. The next turn shows it under the address and a run that carries
/// the set shows it too, so exploring happens once and the recipe is followed after that.
/// </summary>
internal sealed class ReferenceNoteToolHost(
    IReadOnlyDictionary<string, ReferenceSetSandbox> references, IReferenceNotes notes) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
        [AIFunctionFactory.Create(NoteReference, name: "note_reference")];

    [Description("Replaces the note of one upload — the reference:<name> address. Write what it is, the exact commands "
        + "and versions that worked (relative to the upload's folder), what it needs (services, keys by name), and what "
        + "you saw. Every later turn shows the note under the address, and a run that carries the upload shows it too; "
        + "follow it there instead of rediscovering. Replace it when it stops being true.")]
    public async Task<string> NoteReference(
        [Description("The upload's address, reference:<name>.")] string reference,
        [Description("The whole note, replacing the one before; at most 8000 characters.")] string note,
        CancellationToken ct = default)
    {
        if (!references.TryGetValue(reference ?? string.Empty, out var set))
            return $"Error: '{reference}' is not an upload of this conversation. Uploads: [{string.Join(", ", references.Keys)}].";
        if (string.IsNullOrWhiteSpace(note)) return "Error: the note is empty.";
        if (note.Length > IReferenceNotes.MaxChars)
            return $"Error: the note is {note.Length} characters, over the {IReferenceNotes.MaxChars}-character limit.";
        return await notes.SetAsync(set.ConversationId, set.SetId, note, ct)
            ? $"Noted for {reference}; later turns and runs that carry it show this note."
            : $"Error: {reference} is no longer stored in this conversation.";
    }
}
