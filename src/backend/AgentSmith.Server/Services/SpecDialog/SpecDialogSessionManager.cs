using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Opens, continues, closes, resumes and forks spec-dialog sessions keyed by
/// chat thread. State lives in the relational system-of-record (NOT the
/// volatile Redis conversation cache) so transcripts survive a Redis flush.
/// </summary>
public sealed class SpecDialogSessionManager(
    SpecDialogSessionRepository repository,
    TimeProvider timeProvider,
    ILogger<SpecDialogSessionManager> logger)
{
    /// <summary>
    /// Opens a fresh session on the thread. Any session already open on the
    /// same thread is closed first — opening over an existing session IS the
    /// fork ("/spec new" on a hard topic pivot).
    /// </summary>
    public async Task<ConversationState> OpenAsync(
        string platform, string channelId, string threadId, string userId,
        ActiveScope scope, CancellationToken ct)
    {
        await repository.CloseOpenForThreadAsync(platform, threadId, ct);

        var session = new SpecDialogSession
        {
            SessionId = Guid.NewGuid().ToString("N")[..8],
            Platform = platform, ChannelId = channelId, ThreadId = threadId,
            UserId = userId, Project = scope.Project,
            ReposJson = SpecDialogSessionMapper.WriteRepos(scope.Repos),
            LastActivityAt = timeProvider.GetUtcNow(),
        };
        await repository.AddAsync(session, ct);

        logger.LogInformation(
            "Opened spec-dialog session {SessionId} for thread {ThreadId} on {Platform} (scope {Project})",
            session.SessionId, threadId, platform, scope.Project);
        return SpecDialogSessionMapper.ToState(session);
    }

    public async Task<ConversationState?> GetOpenByThreadAsync(
        string platform, string threadId, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        return session is null ? null : SpecDialogSessionMapper.ToState(session);
    }

    /// <summary>
    /// Appends a turn to the open session of the given thread and persists it.
    /// Returns the updated state, or null when the thread has no open session.
    /// </summary>
    public async Task<ConversationState?> AppendTurnAsync(
        string platform, string threadId, TranscriptRole role, string text,
        CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return null;

        var turn = new TranscriptTurn(role, text, timeProvider.GetUtcNow());
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. transcript, turn]);
        session.LastActivityAt = turn.At;
        await repository.SaveAsync(ct);

        return SpecDialogSessionMapper.ToState(session);
    }

    /// <summary>
    /// Re-binds the session with the given id to the current thread and reopens
    /// it, so the conversation continues where it left off.
    /// </summary>
    public async Task<ConversationState?> ResumeAsync(
        string sessionId, string platform, string channelId, string threadId,
        CancellationToken ct)
    {
        var session = await repository.GetBySessionIdAsync(sessionId, ct);
        if (session is null) return null;

        await repository.CloseOpenForThreadAsync(platform, threadId, ct);
        session.Platform = platform;
        session.ChannelId = channelId;
        session.ThreadId = threadId;
        session.IsOpen = true;
        session.LastActivityAt = timeProvider.GetUtcNow();
        await repository.SaveAsync(ct);

        logger.LogInformation(
            "Resumed spec-dialog session {SessionId} into thread {ThreadId} on {Platform}",
            sessionId, threadId, platform);
        return SpecDialogSessionMapper.ToState(session);
    }

    public async Task<IReadOnlyList<ConversationState>> ListOpenAsync(
        string platform, CancellationToken ct) =>
        [.. (await repository.ListOpenAsync(platform, ct)).Select(SpecDialogSessionMapper.ToState)];

    public Task CloseAsync(string platform, string threadId, CancellationToken ct) =>
        repository.CloseOpenForThreadAsync(platform, threadId, ct);
}
