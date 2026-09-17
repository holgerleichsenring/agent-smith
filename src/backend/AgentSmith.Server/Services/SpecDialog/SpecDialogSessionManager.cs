using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Opens, continues, closes and forks spec-dialog sessions keyed by chat thread; a resume
/// is <see cref="SpecDialogResumer"/>. State lives in the relational system-of-record (NOT the
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
        SpecDialogTurnKind? kind, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return null;

        var turn = new TranscriptTurn(role, text, timeProvider.GetUtcNow(), kind);
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. transcript, turn]);
        session.LastActivityAt = turn.At;
        await repository.SaveAsync(ct);

        return SpecDialogSessionMapper.ToState(session);
    }

    /// <summary>
    /// The caller's own open sessions. Listing another person's serves nothing now that only
    /// its owner can resume it, and it disclosed the ids, projects and activity times of every
    /// conversation on the platform to anyone who asked.
    /// </summary>
    public async Task<IReadOnlyList<ConversationState>> ListOpenAsync(
        string userId, string platform, CancellationToken ct) =>
        [.. (await repository.ListOpenAsync(platform, ct))
            .Where(session => string.Equals(session.UserId, userId, StringComparison.Ordinal))
            .Select(SpecDialogSessionMapper.ToState)];

    public Task CloseAsync(string platform, string threadId, CancellationToken ct) =>
        repository.CloseOpenForThreadAsync(platform, threadId, ct);
}
