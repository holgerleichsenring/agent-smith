using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-15-06fc: the design master's draft as files — write_file, edit_file and delete_file over the turn's
/// proposal store. Each change is checked at once by the check of its file's kind and either replaces the draft
/// whole or leaves it standing; a change that leaves the text as it was stores nothing, so an offer stands.
/// 2026-11-19-3d7e: a stored spec or ticket file is then checked for its evidence or required tickets by
/// <paramref name="findings"/> over <paramref name="pipeline"/>, and the answer names what to fix.
/// </summary>
public sealed class DraftFileToolHost(
    ProposalStore store, DraftFileWrite write, DraftScope scope,
    DraftWriteFindings? findings = null, PipelineContext? pipeline = null) : IToolHost
{
    public const string WriteName = "write_file";
    public const string EditName = "edit_file";
    public const string DeleteName = "delete_file";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteFile, name: WriteName), AIFunctionFactory.Create(EditFile, name: EditName),
            AIFunctionFactory.Create(DeleteFile, name: DeleteName)];
    }

    [Description(
        "Writes one file of your draft, replacing it if it exists; read_file and directory_tree read the draft at "
        + "draft:. The draft is files: specs/<id>.yaml is a spec, its file name its id; epic/<id>.yaml is an epic's "
        + "parent over the specs; contract.yaml the epic's series contract; tickets/<key>.yaml a ticket; bug.yaml a "
        + "small fix; comment.md a comment on the ticket this conversation is bound to. A draft is one kind: one spec, "
        + "an epic, tickets, a bug or a comment. The answer is the check of the draft with this file: stored, or "
        + "exactly why not, and then the draft stays as it was.")]
    private Task<string> WriteFile(
        [Description("The file's draft path, such as draft:specs/server-foundation.yaml. A spec's name is a short label "
            + "of lower-case letters, digits and '-', and its requires name siblings by it; filing gives every spec its "
            + "series id.")] string path,
        [Description(DraftFileFormats.Spec + DraftFileFormats.Others)] string content,
        CancellationToken ct = default) =>
        Change(path, _ => (content ?? string.Empty, null), ct);

    [Description(
        "Replaces an exact string in one file of your draft: old_string appears exactly once in it, or every "
        + "occurrence is replaced with replace_all. Revising a file in place keeps its id and every other file as it "
        + "is, so the review looks again only at what changed. The answer is the check of the draft with the edit.")]
    private Task<string> EditFile(
        [Description("The file's draft path, such as draft:specs/server-foundation.yaml.")] string path,
        [Description("The exact text to replace, with enough around it to be unique.")] string old_string,
        [Description("The text to put in its place.")] string new_string,
        [Description("True replaces every occurrence. Default false.")] bool replace_all = false,
        CancellationToken ct = default) =>
        Change(path, current => current is null ? (null, $"{Shown(path)} is no file of this draft")
            : string.IsNullOrEmpty(old_string) ? (null, "old_string is empty")
            : Replaced(current, old_string, new_string, replace_all, path), ct);

    [Description(
        "Deletes one file of your draft, or a directory of it: draft:specs/, draft:tickets/, or draft: for the whole "
        + "draft. Deleting draft: itself discards the draft: the proposal awaiting the operator's decision is withdrawn "
        + "when the turn ends, unless you write the draft again. The answer is the check of the draft without it.")]
    private string DeleteFile([Description("The draft path of the file or directory.")] string path)
    {
        var unchanged = false;
        var refusal = store.Apply(draft =>
        {
            var result = write.Delete(draft, path ?? string.Empty);
            unchanged = result.Next is { } after && DraftFiles.Of(after).SequenceEqual(DraftFiles.Of(draft));
            return unchanged ? DraftWrite.Refused("unchanged") : result;
        }, discards: DraftPath.Bare(path).Length == 0);
        return unchanged ? $"Nothing deleted: {Shown(path)} holds no file."
            : DraftWriteAnswer.Of(refusal, $"Deleted {Shown(path)}.", store);
    }

    private async Task<string> Change(string path, Func<string?, (string? Text, string? Error)> next, CancellationToken ct)
    {
        var bare = DraftPath.Bare(path);
        var (unchanged, note) = (false, string.Empty);
        ProposalDraft? stored = null;
        var refusal = store.Apply(draft =>
        {
            var before = DraftFiles.Find(draft, bare)?.Text;
            var (text, error) = next(before);
            if (text is null) return DraftWrite.Refused(error!);
            var result = write.Put(draft, bare, text, scope);
            if (result.Next is not { } after) return result;
            var storedText = DraftFiles.Find(after, bare)?.Text;
            unchanged = storedText == before;
            note = DraftStoredNote.Of(bare, text, storedText, after);
            stored = after;
            return unchanged ? DraftWrite.Refused("unchanged") : result;
        });
        if (unchanged) return $"{DraftPath.Shown(bare)} is unchanged: its text is as it was.";
        var answer = DraftWriteAnswer.Of(refusal, $"Stored {DraftPath.Shown(bare)}.", store);
        if (refusal is not null || stored is null) return answer;
        var found = findings is null || pipeline is null ? string.Empty : await findings.OfAsync(pipeline, stored, bare, ct);
        return answer + note + found;
    }

    private static (string? Text, string? Error) Replaced(string current, string oldString, string? newString, bool all, string path)
    {
        var (content, _, error) = ExactTextEdit.Apply(current, oldString, newString ?? string.Empty, all, Shown(path));
        return error is null ? (content, null) : (null, error);
    }

    private static string Shown(string? path) => DraftPath.Shown(DraftPath.Bare(path));
}
