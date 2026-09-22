using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What happens between one design turn and the next when the operator asked for an EDIT
/// rather than approving or rejecting (p0315c).
/// <para>
/// The state the finished turn ran on is behind by exactly that turn: it is re-read here, and
/// the proposal being revised is carried onto it so the re-prompted master sees both the note
/// as the latest user turn and what it is editing. A session closed while the edit was pending
/// has nothing to re-run and returns null.
/// </para>
/// <para>
/// 2026-09-22-355b: the note is carried as a VALUE and appended here when the refreshed
/// transcript does not already end with it. A note TYPED into the thread was appended on its
/// way in by the answer admission, so nothing is appended twice; an answer CLICKED on a chat
/// surface completes the pending question inside that surface's adapter and is published
/// straight onto the transport, so it never passes the one place that appends. Relying on the
/// append was a precondition only one path satisfied: the other re-ran a byte-identical
/// transcript, spent a full master loop plus a proposal review on it, and returned the same
/// cut under an acknowledgement saying the proposal was being revised.
/// </para>
/// <para>
/// Its own type because the router had reached the length limit and this is the piece of it
/// that is not routing: a turn's re-entry, with its own read, its own stop condition and its
/// own two log lines.
/// </para>
/// </summary>
public sealed class SpecDialogEditReload(
    SpecDialogSessionManager sessions, ILogger<SpecDialogEditReload> logger)
{
    /// <summary>The state the next turn runs on, or null when the session is gone.</summary>
    public async Task<ConversationState?> RefreshedAsync(
        ConversationState current, OutcomeProposal revising, string note, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(current);
        var refreshed = current.ThreadId is null
            ? null
            : await sessions.GetOpenByThreadAsync(current.Platform, current.ThreadId, ct);
        if (refreshed is null)
        {
            logger.LogWarning(
                "Session {SessionId} closed while an outcome edit was pending — stopping",
                current.JobId);
            return null;
        }
        refreshed = await CarryingTheNoteAsync(refreshed, note, ct);
        if (refreshed is null)
        {
            logger.LogWarning(
                "Session {SessionId} closed while its edit note was being carried — stopping",
                current.JobId);
            return null;
        }
        logger.LogInformation(
            "Re-running design turn for session {SessionId} with the operator's edit note",
            current.JobId);
        return refreshed with { Revising = revising };
    }

    /// <summary>
    /// The state whose transcript ends in the note — the one it already was when the note was
    /// typed, or the one it becomes when the note arrived off the transcript.
    /// </summary>
    private async Task<ConversationState?> CarryingTheNoteAsync(
        ConversationState refreshed, string note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note) || EndsWithTheNote(refreshed, note)) return refreshed;
        logger.LogInformation(
            "Appending the edit note to session {SessionId} — it was answered off the transcript",
            refreshed.JobId);
        return await sessions.AppendTurnAsync(
            refreshed.Platform, refreshed.ThreadId!, TranscriptRole.User, note, null, null, ct);
    }

    private static bool EndsWithTheNote(ConversationState refreshed, string note) =>
        refreshed.Transcript.Count > 0
        && refreshed.Transcript[^1] is { Role: TranscriptRole.User } last
        && string.Equals(last.Text.Trim(), note.Trim(), StringComparison.Ordinal);
}
