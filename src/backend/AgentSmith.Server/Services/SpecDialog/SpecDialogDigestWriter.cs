using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Folds the older turns of a conversation that has grown past <see cref="TranscriptWindow.VerbatimChars"/> into one
/// digest turn, written by the summarization model and appended as an ordinary assistant turn. Every stored turn
/// stays; the window reads the digest in their place.
/// <para>
/// It runs after the turn gate is released, so the row is read again right before the append: a message admitted
/// during the call is kept, and a conversation forked away from on the same thread gets nothing. It swallows every
/// failure but the caller's cancellation — a failed digest leaves the transcript, and the next kept reply retries.
/// </para>
/// </summary>
public sealed class SpecDialogDigestWriter(
    IChatClientFactory chatClients,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    SpecDialogSessionRepository repository,
    BoundSessionNudge bound,
    TimeProvider timeProvider,
    ILogger<SpecDialogDigestWriter> logger)
{
    /// <summary>A digest is a summary, so the ceiling is a summary's worth of tokens.</summary>
    internal const int MaxOutputTokens = 4000;

    public async Task WriteAsync(ConversationState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ThreadId is null) return;
        try
        {
            if (await SameSessionAsync(state, ct) is not { } session) return;
            var turns = SpecDialogTurnSeeds.MapTranscript(SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson));
            if (TranscriptWindow.Fold(turns) is not { } fold) return;
            var digest = await SummarizeAsync(state, fold, ct);
            if (digest.Length == 0)
            {
                logger.LogInformation("The digest of session {SessionId} came back empty and was discarded", state.JobId);
                return;
            }
            await AppendAsync(state, fold, digest, ct);
        }
        // On the caller's token, as the subject mint: an LLM timeout arrives as a cancellation on an uncancelled token.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The digest of session {SessionId} could not be written", state.JobId);
        }
    }

    private async Task<string> SummarizeAsync(ConversationState state, TranscriptFold fold, CancellationToken ct)
    {
        var agent = SpecDialogSubjectMinter.AgentOf(configLoader, serverContext, state.Project);
        var response = await chatClients.Create(agent, TaskType.Summarization).GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SpecDialogDigestInstruction.Text),
                new ChatMessage(ChatRole.User, SpecDialogDigestInstruction.Exchange(fold.PreviousDigest, fold.Turns)),
            ],
            new ChatOptions { MaxOutputTokens = MaxOutputTokens }, ct);
        // Outside any run, as the subject mint: nothing else accounts this call, so it is tracked into the log line.
        var cost = new PipelineCostTracker(config: agent.Pricing);
        cost.Track(response);
        logger.LogInformation(
            "Digested turns {From} to {Through} of session {SessionId} for {Cost}",
            fold.From + 1, fold.Through + 1, state.JobId, cost.EstimateCostUsd());
        return SpecDialogDraftBlocks.Strip(response.Text).Trim();
    }

    private async Task AppendAsync(ConversationState state, TranscriptFold fold, string digest, CancellationToken ct)
    {
        if (await SameSessionAsync(state, ct) is not { } session)
        {
            logger.LogInformation("Session {SessionId} was closed during its digest; nothing was appended", state.JobId);
            return;
        }
        var turn = new TranscriptTurn(
            TranscriptRole.Assistant, $"{SpecDialogDigestInstruction.Heading(fold.From, fold.Through)}\n\n{digest}",
            timeProvider.GetUtcNow(), SpecDialogTurnKind.Digest,
            Author: SpecDialogDigestInstruction.Author, DigestThrough: fold.Through);
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. transcript, turn]);
        await bound.SaveAsync(session, ct);
    }

    /// <summary>The open session of the thread, read from the database, while it is still this state's session.</summary>
    private async Task<SpecDialogSession?> SameSessionAsync(ConversationState state, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(state.Platform, state.ThreadId!, ct);
        return session is not null && session.SessionId == state.JobId ? session : null;
    }
}
