using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
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
    IHeldSandboxRegister holds,
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
        await CloseAndReleaseAsync(platform, threadId, ct);

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
    /// 2026-09-17-042el: the decision is required like the kind, null for any turn that is not
    /// an approval answer.
    /// </summary>
    public async Task<ConversationState?> AppendTurnAsync(
        string platform, string threadId, TranscriptRole role, string text,
        SpecDialogTurnKind? kind, SpecDialogDecision? decision, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return null;

        var turn = new TranscriptTurn(role, text, timeProvider.GetUtcNow(), kind, decision);
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. transcript, turn]);
        session.LastActivityAt = turn.At;
        await repository.SaveAsync(ct);

        return SpecDialogSessionMapper.ToState(session);
    }

    /// <summary>
    /// 2026-09-17-042el: takes the decision back from a stored turn whose answer answered nothing —
    /// the latest turn equal to <paramref name="turn"/>, so a message stored after it is not touched.
    /// Returns the updated state, or null when the thread has no open session.
    /// </summary>
    public async Task<ConversationState?> ClearDecisionAsync(
        string platform, string threadId, TranscriptTurn turn, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return null;

        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).ToList();
        var index = transcript.LastIndexOf(turn);
        if (index < 0) return SpecDialogSessionMapper.ToState(session);
        transcript[index] = turn with { Decision = null };
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript(transcript);
        await repository.SaveAsync(ct);

        return SpecDialogSessionMapper.ToState(session);
    }

    public Task CloseAsync(string platform, string threadId, CancellationToken ct) =>
        CloseAndReleaseAsync(platform, threadId, ct);

    /// <summary>
    /// 2026-09-22-2d11b: a conversation that is closed — or forked away from, which closes it
    /// first — lets go of the sandboxes it was holding between turns. The teardown is detached
    /// from whoever asked for it: a fork closes the old session on the way to opening its
    /// successor, and nobody should wait for containers on that path.
    /// </summary>
    private async Task CloseAndReleaseAsync(string platform, string threadId, CancellationToken ct)
    {
        var open = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        await repository.CloseOpenForThreadAsync(platform, threadId, ct);
        if (open is not null) _ = holds.ReleaseConversationAsync(open.SessionId, CancellationToken.None);
    }
}
