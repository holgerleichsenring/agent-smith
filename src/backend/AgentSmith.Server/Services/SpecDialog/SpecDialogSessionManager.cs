using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Opens, continues, closes and forks spec-dialog sessions keyed by chat thread; a resume is <see cref="SpecDialogResumer"/>.
/// State lives in the relational system-of-record (not the Redis cache) so transcripts survive a Redis flush.
/// </summary>
public sealed class SpecDialogSessionManager(
    SpecDialogSessionRepository repository, BoundSessionNudge bound,
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
        ActiveScope scope, CancellationToken ct, TicketBinding? ticket = null)
    {
        await CloseAndReleaseAsync(platform, threadId, ct);

        var session = new SpecDialogSession
        {
            SessionId = Guid.NewGuid().ToString("N")[..8],
            Platform = platform, ChannelId = channelId, ThreadId = threadId,
            UserId = userId, Project = scope.Project,
            ReposJson = SpecDialogSessionMapper.WriteRepos(scope.Repos),
            LastActivityAt = timeProvider.GetUtcNow(),
            Tracker = ticket?.Tracker,
            TicketKey = ticket?.Key,
            // 2026-09-25-8e51b: a bound conversation is NAMED before its first exchange, so the
            // mint — which only runs on a session with no subject — never runs for one. Trimmed
            // rather than admitted: the admission rule refuses brackets and quotes because it
            // judges a MODEL's answer, and a real ticket title routinely carries both.
            Subject = SpecDialogSessionMapper.Heading(ticket?.Title),
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
    /// an approval answer. The author is the display name of the person who wrote it, if known.
    /// </summary>
    public async Task<ConversationState?> AppendTurnAsync(
        string platform, string threadId, TranscriptRole role, string text,
        SpecDialogTurnKind? kind, SpecDialogDecision? decision, CancellationToken ct, string? author = null, string? runId = null,
        string? decisionProposalId = null) // 2026-11-11-205fe: the proposal a decision names
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return null;

        var turn = new TranscriptTurn(role, text, timeProvider.GetUtcNow(), kind, decision, Author: author, RunId: runId, DecisionProposalId: decision is null ? null : decisionProposalId);
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. transcript, turn]);
        session.LastActivityAt = turn.At;
        await bound.SaveAsync(session, ct);

        return SpecDialogSessionMapper.ToState(session);
    }

    /// <summary>
    /// 2026-09-17-042el: settles the decision on a stored turn — the latest turn equal to <paramref name="turn"/>, so a
    /// message stored after it is not touched: applied marks it (205fe review), otherwise it is taken back.
    /// Returns the updated state, or null when the thread has no open session.
    /// </summary>
    public async Task<ConversationState?> SettleDecisionAsync(
        string platform, string threadId, TranscriptTurn turn, bool applied, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        if (session is null) return null;

        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).ToList();
        var index = transcript.LastIndexOf(turn);
        if (index < 0) return SpecDialogSessionMapper.ToState(session);
        transcript[index] = applied ? turn with { DecisionApplied = true } : turn with { Decision = null, DecisionProposalId = null };
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript(transcript);
        await bound.SaveAsync(session, ct);

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
