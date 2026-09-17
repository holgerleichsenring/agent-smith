using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Moves a session onto the current thread and reopens it, closing whatever was open there.
/// A dialog id is a tab, not a conversation: the dashboard opens a past conversation by
/// minting a fresh id and resuming onto it, which closes nothing.
/// </summary>
public sealed class SpecDialogResumer(
    SpecDialogSessionRepository repository,
    SpecDialogTurnGate turnGate,
    SpecDialogPendingQuestions pendingQuestions,
    TimeProvider timeProvider,
    ILogger<SpecDialogResumer> logger)
{
    /// <summary>
    /// 2026-09-15-9033: a session is resumed by its OWNER or by nobody. The lookup is by
    /// session id alone, and the resume rewrites where the session lives, so without the
    /// check a /spec resume typed in any chat thread takes a session from whoever opened it.
    /// An unowned session answers "not found", the reply for an id that never existed.
    /// <para>
    /// A session mid-turn is not moved. The running turn holds the old thread: its reply
    /// would find no open session and never be stored, and its approval would be accepted in
    /// the new tab and then fail to file for want of an open session. The gate is held for
    /// the move itself, so no turn starts between the check and the rewrite.
    /// </para>
    /// </summary>
    public async Task<SpecDialogResumeResult> ResumeAsync(
        string sessionId, string userId, string platform, string channelId, string threadId,
        CancellationToken ct)
    {
        var session = await repository.GetBySessionIdAsync(sessionId, ct);
        if (session is null || !string.Equals(session.UserId, userId, StringComparison.Ordinal))
            return new SpecDialogResumeNotFound();
        if (pendingQuestions.TryPeek(sessionId, out _))
            return new SpecDialogResumeRefused(sessionId, QuestionPending: true);
        if (!turnGate.TryEnter(sessionId))
            return new SpecDialogResumeRefused(sessionId, QuestionPending: false);
        try
        {
            return new SpecDialogResumed(await MoveAsync(session, platform, channelId, threadId, ct));
        }
        finally
        {
            turnGate.Exit(sessionId);
        }
    }

    private async Task<ConversationState> MoveAsync(
        SpecDialogSession session, string platform, string channelId, string threadId,
        CancellationToken ct)
    {
        // Already here, it has nothing to move. Closing the target thread would close THIS row in
        // the database while the tracked copy still read open, so setting it open again would be
        // no change to save — a resume into the thread a session already lives in closed it.
        var alreadyHere = session.IsOpen
            && string.Equals(session.Platform, platform, StringComparison.OrdinalIgnoreCase)
            && string.Equals(session.ThreadId, threadId, StringComparison.Ordinal);
        if (!alreadyHere) await repository.CloseOpenForThreadAsync(platform, threadId, ct);
        session.Platform = platform;
        session.ChannelId = channelId;
        session.ThreadId = threadId;
        session.IsOpen = true;
        session.LastActivityAt = timeProvider.GetUtcNow();
        await repository.SaveAsync(ct);

        logger.LogInformation(
            "Resumed spec-dialog session {SessionId} into thread {ThreadId} on {Platform}",
            session.SessionId, threadId, platform);
        return SpecDialogSessionMapper.ToState(session);
    }
}
