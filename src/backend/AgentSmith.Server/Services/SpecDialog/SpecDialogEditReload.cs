using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What happens between one design turn and the next when the operator asked for an EDIT
/// rather than approving or rejecting (p0315c).
/// <para>
/// The note arrived as an ordinary thread message and was already appended to the durable
/// transcript by its own inbound routing, so the state the finished turn ran on is now behind
/// by exactly that turn: it is re-read here, and the proposal being revised is carried onto it
/// so the re-prompted master sees both the note as the latest user turn and what it is editing.
/// A session closed while the edit was pending has nothing to re-run and returns null.
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
        ConversationState current, OutcomeProposal revising, CancellationToken ct)
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
        logger.LogInformation(
            "Re-running design turn for session {SessionId} with the operator's edit note",
            current.JobId);
        return refreshed with { Revising = revising };
    }
}
