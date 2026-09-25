using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The in-thread confirmation gate. Shows the proposed outcome as a
/// DialogQuestion(Approval) through the platform's generic approval
/// blocks/cards (threaded), marks it pending so the thread's next message
/// routes back as the answer over the dialogue transport, and interprets the
/// reply: explicit approval files, explicit rejection files nothing, any
/// other text is an edit note for the master, silence times out.
/// <para>
/// 2026-09-22-355b: beside the approve/reject pair it offers the other SHAPES this
/// proposal's kind could take (<see cref="OutcomeShapes"/>), so the third door is visible
/// rather than known. The question stays an Approval — the pair, the recorded decision and
/// the pane's counted summary are all keyed off that kind — and a picked shape rides back as
/// ordinary text, which is already an edit note carrying that label.
/// </para>
/// </summary>
public sealed class SpecDialogOutcomeConfirmer(
    IDialogueTransport dialogueTransport,
    SpecDialogMessenger messenger,
    SpecDialogPendingQuestions pendingQuestions,
    SpecDialogOutcomeComposer composer,
    ILogger<SpecDialogOutcomeConfirmer> logger)
{
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(15);

    public async Task<ConfirmationResult> ConfirmAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        var question = new DialogQuestion(
            Guid.NewGuid().ToString("N"), QuestionType.Approval,
            composer.ComposeConfirmation(proposal).In(SpecDialogMarkup.For(state.Platform)),
            Context: null, Choices: OutcomeShapes.For(proposal, Bound(state)), DefaultAnswer: "",
            ConfirmationTimeout);

        // The thread's next text message is the answer (router pending branch);
        // the buttons are the same question's second input surface.
        pendingQuestions.Set(
            state.JobId, question, DateTimeOffset.UtcNow + ConfirmationTimeout);
        using var buttonCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buttons = RelayButtonAnswerAsync(state, question, buttonCts.Token);
        try
        {
            var answer = await dialogueTransport.WaitForAnswerAsync(
                state.JobId, question.QuestionId, ConfirmationTimeout, cancellationToken);
            return Interpret(state, answer);
        }
        finally
        {
            buttonCts.Cancel();
            await buttons;
            // A timed-out confirmation must not swallow the thread's next
            // design message as a stale answer.
            pendingQuestions.Clear(state.JobId);
        }
    }

    // Posts the approval blocks/cards threaded and bridges a button click back
    // onto the dialogue transport, where the main wait picks it up like any
    // text answer. Never throws — a dead chat API degrades to text replies.
    private async Task RelayButtonAnswerAsync(
        ConversationState state, DialogQuestion question, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await messenger.AskQuestionAsync(
                state.Platform, state.ChannelId, state.ThreadId!, question, cancellationToken);
            if (answer is not null)
                await dialogueTransport.PublishAnswerAsync(state.JobId, answer, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // A text reply won the race — the buttons are moot.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Approval buttons unavailable for spec-dialog session {SessionId} — "
                + "text replies in the thread still work", state.JobId);
        }
    }

    private ConfirmationResult Interpret(ConversationState state, DialogAnswer? answer)
    {
        if (answer is null)
        {
            logger.LogWarning(
                "Outcome confirmation for spec-dialog session {SessionId} timed out — nothing routes",
                state.JobId);
            return new OutcomeConfirmationTimedOut();
        }
        // 2026-09-22-355b: the order of matching IS the contract. An approval or rejection word
        // first, then everything else — an offered shape's label included — as an edit note
        // carrying that text, which is what sends the master round again in that shape.
        // 2026-09-25-8e51e: the amendment is read BETWEEN the two, and only where it was
        // offered: a conversation that belongs to no ticket has no ticket to amend, so the same
        // words there are an ordinary edit note.
        return SpecDialogAnswerWords.DecisionIn(answer.Answer) switch
        {
            SpecDialogDecision.Approved => new OutcomeConfirmed(),
            SpecDialogDecision.Rejected => new OutcomeRejected(),
            _ when Bound(state) && IsAmend(answer.Answer) => new OutcomeAmendRequested(),
            _ => new OutcomeEditRequested(answer.Answer.Trim()),
        };
    }

    /// <summary>The conversation belongs to a ticket — 2026-09-25-8e51b's binding.</summary>
    private static bool Bound(ConversationState state) => state.TicketKey is not null;

    private static bool IsAmend(string answer) =>
        string.Equals(answer.Trim(), OutcomeShapes.AmendTheTicket.Label, StringComparison.OrdinalIgnoreCase);
}
