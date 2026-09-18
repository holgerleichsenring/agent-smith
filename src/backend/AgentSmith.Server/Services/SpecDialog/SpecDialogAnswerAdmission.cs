using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042el: a thread message after it is stored. <paramref name="Answered"/> says it went
/// to the question a running turn is blocked on; otherwise it is a message for the turn gate.
/// </summary>
public sealed record SpecDialogAdmitted(ConversationState State, bool Answered);

/// <summary>
/// 2026-09-17-042el: stores a thread message and, when a turn is blocked on a question, hands it
/// over as the answer — recording on the transcript what an approval answer decided.
/// <para>
/// The gate is not touched: a question is pending only while a turn holds it. The question is
/// peeked before the append and taken after it, so an append that throws leaves it answerable,
/// and published last, so an edit note is in the transcript the re-run turn reads. Only the
/// peeked question is taken: one that expired, was answered by a second message, or was replaced
/// meanwhile answered nothing here, and a decision stored for it is taken back.
/// </para>
/// </summary>
public sealed class SpecDialogAnswerAdmission(
    SpecDialogSessionManager sessions,
    SpecDialogPendingQuestions pendingQuestions,
    IDialogueTransport dialogueTransport)
{
    /// <summary>The stored message and whether it answered; null when the thread has no open session.</summary>
    public async Task<SpecDialogAdmitted?> AdmitAsync(
        string text, string userId, string platform, string threadId, CancellationToken ct)
    {
        var open = await sessions.GetOpenByThreadAsync(platform, threadId, ct);
        if (open is null) return null;
        var asked = pendingQuestions.TryPeek(open.JobId, out var peeked) ? peeked : null;
        var decision = asked is null ? null : SpecDialogAnswerWords.DecisionOn(asked.Question, text);
        var state = await sessions.AppendTurnAsync(
            platform, threadId, TranscriptRole.User, text, null, decision, ct);
        if (state is null) return null;
        if (asked is null || !pendingQuestions.TryTake(state.JobId, asked))
            return new(await UndecidedAsync(state, platform, threadId, ct), Answered: false);

        await dialogueTransport.PublishAnswerAsync(
            state.JobId,
            new DialogAnswer(asked.Question.QuestionId, text, null, DateTimeOffset.UtcNow, userId), ct);
        return new(state, Answered: true);
    }

    private async Task<ConversationState> UndecidedAsync(
        ConversationState state, string platform, string threadId, CancellationToken ct) =>
        state.Transcript[^1].Decision is null
            ? state
            : await sessions.ClearDecisionAsync(platform, threadId, state.Transcript[^1], ct) ?? state;
}
