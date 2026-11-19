using System.ComponentModel;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;
using static AgentSmith.Application.Services.Tools.MergedTools;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-18-9c1cd: the design partner's and the steward's merged tools, each over the functions it replaces (looked
/// up by their old names in what the factories built this turn) and each offered on every turn of its conversation:
/// a part the turn was not seeded with answers why instead of disappearing.
/// </summary>
public static partial class MergedDialogTools
{
    /// <summary>Why a person's act is refused when the poster of this turn may not start runs.</summary>
    public const string PosterMayNot =
        "Not done: the person who wrote this message may not start runs (they lack runs.control on this project). "
        + "Say so in your reply; someone who may start runs can ask for it.";

    /// <summary>run_command over the work clones and the uploads' containers (run_in_reference).</summary>
    public static AIFunction RunCommand(AIFunction? work, AIFunction? reference, bool mayStartRuns) =>
        AIFunctionFactory.Create(
            async ([Description("The shell command (/bin/sh -c).")] string command,
                   [Description("The repository, by the name the read tools use — or reference:<name> to run in the container that holds a copy of that upload.")] string repo,
                   [Description("'full' for the whole history of a repository's clone, best on its first command; omit otherwise.")] string? history = null,
                   [Description("Seconds before the command is stopped; clamped to the operator's step cap.")] int? timeout_seconds = null,
                   CancellationToken ct = default) =>
                (repo ?? string.Empty).StartsWith(Specs.ReferenceScopeName.Prefix, StringComparison.Ordinal)
                    ? await Call(reference, $"Error: nothing can run in '{repo}': this conversation has no upload of that name yet, "
                        + "or this installation runs sandboxes in-process, where a command would run inside the server.", ct,
                        ("reference", repo), ("command", command), ("timeout_seconds", timeout_seconds))
                    : await Call(work, mayStartRuns
                        ? "Error: this installation gives a conversation no work clone (sandboxes run in-process, or share one "
                          + "credential), so commands run only in an upload's container (repo reference:<name>). Cite what the read tools show."
                        : PosterMayNot, ct,
                        ("command", command), ("repo", repo), ("history", history), ("timeout_seconds", timeout_seconds)),
            "run_command",
            "Runs a shell command (/bin/sh -c). With a repository: in your throwaway work clone of the commit your reads "
            + "show, on the repository's toolchain image, /work as its root — check a claim, git log -p, build, run the "
            + "tests, try a change; nothing is pushed or kept, and the clone is gone when the turn ends. With "
            + "reference:<name>: in the disposable container holding a copy of that upload under /work/<name>/ — inspect, "
            + "install, start, request; nothing changes what was uploaded. Start a server detached (nohup … &). Returns "
            + "exit_code, elapsed_ms, stdout and stderr.");

    /// <summary>write_file over the draft (and delete_file), the mocks (write_mock) and the notes (write_notes).</summary>
    public static AIFunction WriteFile(IReadOnlyList<AITool> raw, ConversationNotesToolHost? notes, bool steward) =>
        AIFunctionFactory.Create(
            async ([Description("draft:<path> for a file of your draft (draft:specs/<label>.yaml, draft:tickets/<key>.yaml, draft:bug.yaml, draft:comment.md …), mock:<spec_id> for the next version of that spec's HTML mock, or notes: for this conversation's notes.")] string path,
                   [Description("The whole content: the file, the whole HTML page (CSS and script inline), or the whole notes.")] string? content = null,
                   [Description("True deletes the draft file or directory at path (draft:specs/, draft: for the whole draft) instead of writing.")] bool delete = false,
                   CancellationToken ct = default) =>
            {
                var at = (path ?? string.Empty).Trim();
                if (at.StartsWith(NotesPrefix, StringComparison.Ordinal))
                {
                    if (notes is null) return "Error: this conversation keeps no notes here.";
                    return delete ? "Error: notes are replaced, not deleted; write the notes you keep." : await notes.WriteNotes(content ?? string.Empty, ct);
                }
                if (steward) return StewardDrafts;
                if (at.StartsWith(MockPrefix, StringComparison.Ordinal))
                {
                    if (delete) return "Error: a mock version is kept; write the next version instead.";
                    var (spec, _) = MockAddress(at);
                    return await Call(Named(raw, "write_mock"), "Error: this conversation keeps no mocks here.", ct,
                        ("spec_id", spec ?? string.Empty), ("html", content ?? string.Empty));
                }
                return delete
                    ? await Call(Named(raw, DraftFileToolHost.DeleteName), NoDraft, ct, ("path", at))
                    : await Call(Named(raw, DraftFileToolHost.WriteName), NoDraft, ct, ("path", at), ("content", content ?? string.Empty));
            },
            "write_file",
            steward
                ? "Replaces this conversation's notes (path notes:) with the whole text you pass — what the material is, "
                  + "what is settled and what is open; the next turn's prompt carries them. The steward drafts nothing: the series changes through edit_series."
                : "Writes one file of your draft at draft:<path>, replacing it if it exists; the answer is the check of the draft "
                  + "with it: stored, or exactly why not, and the draft stays as it was. A draft is one kind: one spec, an epic, "
                  + "tickets, a bug or a comment. delete: true removes a draft file or directory. mock:<spec_id> stores the whole "
                  + "HTML page as that spec's next mock version, renders it and shows desktop and mobile screenshots. notes: "
                  + "replaces this conversation's notes, which the next turn's prompt carries whole.");

    /// <summary>edit_file over the draft and the notes.</summary>
    public static AIFunction EditFile(IReadOnlyList<AITool> raw, ConversationNotesToolHost? notes, bool steward) =>
        AIFunctionFactory.Create(
            async ([Description("draft:<path> for a file of your draft, or notes: for this conversation's notes.")] string path,
                   [Description("The exact text to replace, with enough around it to be unique.")] string old_string,
                   [Description("The text to put in its place.")] string new_string,
                   [Description("True replaces every occurrence. Default false.")] bool replace_all = false,
                   CancellationToken ct = default) =>
            {
                var at = (path ?? string.Empty).Trim();
                if (at.StartsWith(NotesPrefix, StringComparison.Ordinal))
                {
                    if (notes is null) return "Error: this conversation keeps no notes here.";
                    var current = await notes.ReadNotes(ct);
                    if (current == "No notes yet.") return "Error: there are no notes yet; write them with write_file notes:.";
                    var (text, _, error) = ExactTextEdit.Apply(current, old_string, new_string ?? string.Empty, replace_all, NotesPrefix);
                    return error is not null ? $"Error: {error}" : await notes.WriteNotes(text, ct);
                }
                if (steward) return StewardDrafts;
                return await Call(Named(raw, DraftFileToolHost.EditName), NoDraft, ct,
                    ("path", at), ("old_string", old_string), ("new_string", new_string), ("replace_all", replace_all));
            },
            "edit_file",
            "Replaces an exact string in one file of your draft (draft:<path>) or in this conversation's notes (notes:): "
            + "old_string appears exactly once, or every occurrence is replaced with replace_all. Revising a draft file in "
            + "place keeps its id and every other file as it is; the answer is the check of the draft with the edit.");

    private const string NoDraft = "Error: this turn keeps no draft.";

    private const string StewardDrafts =
        "Error: the steward drafts nothing — draft: and mock: paths are the design partner's. Change the series with "
        + "edit_series; write_file and edit_file here keep this conversation's notes (notes:).";

}
