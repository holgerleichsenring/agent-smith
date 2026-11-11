using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042el: a thread message after it is stored. <paramref name="Answered"/> says it went
/// to the question a running turn is blocked on; otherwise it is a message for the turn gate.
/// 2026-11-10-ccd9: <paramref name="Decision"/> is the approve or reject word it carries, recorded
/// tentatively until the decision path says whether it decided a card.
/// </summary>
public sealed record SpecDialogAdmitted(ConversationState State, bool Answered, SpecDialogDecision? Decision = null);

/// <summary>
/// 2026-09-17-042el: stores a thread message and, when a turn is blocked on a question, hands it
/// over as the answer.
/// <para>
/// The gate is not touched: a question is pending only while a turn holds it. The question is
/// peeked before the append and taken after it, so an append that throws leaves it answerable.
/// 2026-11-10-ccd9: an approval is no longer a pending question. A message that is an approve or
/// reject word is stored WITH that decision before the gate, and <see cref="UndecideAsync"/> takes the
/// decision back when it decided no card — so a message is never lost to a busy gate.
/// </para>
/// </summary>
public sealed class SpecDialogAnswerAdmission(
    SpecDialogSessionManager sessions,
    SpecDialogPendingQuestions pendingQuestions,
    IDialogueTransport dialogueTransport,
    SpecDialogProposalState? proposals = null) // 2026-11-11-205fe
{
    /// <summary>The stored message and whether it answered; null when the thread has no open session.
    /// The author is the poster's display name, recorded on the stored turn. 2026-11-11-205fe: a decided turn names the
    /// proposal it decides — the card it was sent from, else the one awaiting — so a crash before the decision path
    /// ran leaves a decision the view reads as not applied while that proposal still awaits.</summary>
    public async Task<SpecDialogAdmitted?> AdmitAsync(
        string text, string userId, string platform, string threadId, CancellationToken ct,
        string? author = null, string? proposalId = null)
    {
        var open = await sessions.GetOpenByThreadAsync(platform, threadId, ct);
        if (open is null) return null;
        var asked = pendingQuestions.TryPeek(open.JobId, out var peeked) ? peeked : null;
        var decision = asked is null ? SpecDialogAnswerWords.DecisionIn(text) : SpecDialogAnswerWords.DecisionOn(asked.Question, text);
        var named = decision is null || asked is not null ? null // an answer to a live question names no proposal
            : proposalId ?? (proposals is null ? null : (await proposals.ReadAsync(open, ct)).AwaitingId);
        var state = await sessions.AppendTurnAsync(
            platform, threadId, TranscriptRole.User, text, null, decision, ct, author, decisionProposalId: named);
        if (state is null) return null;
        if (asked is null) return new(state, Answered: false, decision);
        if (!pendingQuestions.TryTake(state.JobId, asked))
            return new(await UndecideAsync(state, ct), Answered: false);

        await dialogueTransport.PublishAnswerAsync(
            state.JobId,
            new DialogAnswer(asked.Question.QuestionId, text, null, DateTimeOffset.UtcNow, userId), ct);
        return new(state, Answered: true);
    }

    /// <summary>Takes back the decision recorded on the last stored message; it decided nothing.</summary>
    public Task<ConversationState> UndecideAsync(ConversationState state, CancellationToken ct) =>
        SettleAsync(state, applied: false, ct);

    /// <summary>2026-11-11-205fe review: marks the decision on the last stored message applied — it decided its card.</summary>
    public Task<ConversationState> AppliedAsync(ConversationState state, CancellationToken ct) =>
        SettleAsync(state, applied: true, ct);

    private async Task<ConversationState> SettleAsync(ConversationState state, bool applied, CancellationToken ct) =>
        state.Transcript.Count == 0 || state.Transcript[^1].Decision is null
            ? state
            : await sessions.SettleDecisionAsync(state.Platform, state.ThreadId!, state.Transcript[^1], applied, ct) ?? state;
}
