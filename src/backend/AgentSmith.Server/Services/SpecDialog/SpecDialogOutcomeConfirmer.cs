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
/// </summary>
public sealed class SpecDialogOutcomeConfirmer(
    IDialogueTransport dialogueTransport,
    SpecDialogMessenger messenger,
    SpecDialogPendingQuestions pendingQuestions,
    SpecDialogOutcomeComposer composer,
    ILogger<SpecDialogOutcomeConfirmer> logger)
{
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(15);
    private static readonly string[] ApprovalAnswers =
        ["yes", "y", "approve", "approved", "ok", "confirm", "confirmed"];
    private static readonly string[] RejectionAnswers =
        ["no", "n", "reject", "rejected", "decline", "declined", "cancel", "discard", "drop"];

    public async Task<ConfirmationResult> ConfirmAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        var question = new DialogQuestion(
            Guid.NewGuid().ToString("N"), QuestionType.Approval,
            composer.ComposeConfirmation(proposal),
            Context: null, Choices: null, DefaultAnswer: "", ConfirmationTimeout);

        // The thread's next text message is the answer (router pending branch);
        // the buttons are the same question's second input surface.
        pendingQuestions.Set(state.JobId, question.QuestionId);
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
        var reply = answer.Answer.Trim();
        var token = reply.ToLowerInvariant();
        if (ApprovalAnswers.Contains(token)) return new OutcomeConfirmed();
        if (RejectionAnswers.Contains(token)) return new OutcomeRejected();
        return new OutcomeEditRequested(reply);
    }
}
