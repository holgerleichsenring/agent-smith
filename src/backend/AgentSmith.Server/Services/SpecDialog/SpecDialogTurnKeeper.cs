using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Keeps a design turn's reply in the conversation with the name the master gave it, and, once the turn gate is
/// released, folds a conversation that has grown past its window into a digest. Both masters' turns are kept here.
/// <para>
/// 2026-11-04-96a0b: an upload stored while the turn ran is named in a Notice turn kept BEFORE the reply. The page shows
/// a turn only when it is sent, and the live proposal card attaches to the last agent entry, so the notice is returned
/// for the router to send first, and the reply stays the entry its card and the window's pinned answer read.
/// </para>
/// <para>And the thread the turn's master ended with, for the next turn to replay; a failed turn drops it.</para>
/// <para>2026-11-05-862ea: and the work picture the turn stated, over the stored one.</para>
/// </summary>
public sealed class SpecDialogTurnKeeper(
    SpecDialogSessionManager sessions, SpecDialogSubjectStore subjects, SpecDialogDigestWriter digests,
    SpecDialogUploadArrivals arrivals, SpecDialogThreadStore threads, SpecDialogWorkPictureStore pictures)
{
    /// <summary>Keeps the turn and returns the notice about uploads it did not hold, or null.</summary>
    public async Task<string?> KeepAsync(
        ConversationState state, SpecDialogTurnResult result, string platform, string threadId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        var notice = await arrivals.NoticeAsync(state, result, ct);
        if (notice is not null)
            await sessions.AppendTurnAsync(platform, threadId, TranscriptRole.Assistant, notice, SpecDialogTurnKind.Notice, null, ct);
        var kept = await sessions.AppendTurnAsync( // 2026-11-10-d854c: the agent turn names the run it came from
            platform, threadId, TranscriptRole.Assistant, result.Reply, result.Kind, null, ct, runId: result.RunId);
        // A failed turn's thread would replay a turn that never answered; the next one starts fresh from the transcript.
        if (result.Kind == SpecDialogTurnKind.Failure) await threads.DropAsync(state.JobId, ct);
        else await threads.KeepAsync(state, result, kept, ct);
        // 2026-11-04-96a0a: before the router sends the reply, which is what makes the page re-read the conversation,
        // so a conversation that asks one question and never comes back is still headed with its name.
        if (state.Subject is null && result.Subject is { } subject && result.Kind != SpecDialogTurnKind.Failure)
            await subjects.StoreAsync(platform, threadId, subject, ct);
        // 2026-11-05-862ea: stated or corrected on any turn; it replaces the stored one. 2026-11-10-548f: a failed turn's
        // too, and the higher effort wins — a conversation does not shrink because one turn was small.
        if (result.WorkPicture is { } picture)
            await pictures.StoreAsync(platform, threadId, picture with
            {
                Effort = (ComplexityTier)Math.Max((int)picture.Effort, (int)(state.WorkPicture?.Effort ?? ComplexityTier.Unknown)),
            }, ct);
        return notice;
    }

    /// <summary>Writes the digest that is due, if one is; it never throws but on the caller's cancellation.</summary>
    public Task FoldAsync(ConversationState state, CancellationToken ct) => digests.WriteAsync(state, ct);
}
