using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: the in-thread confirmation gate. Publishes the proposed outcome as
/// a DialogQuestion(Approval) on the dialogue transport (jobId = session id)
/// and reuses the p0315b question machinery for the wait window: the pump
/// relays the question threaded and marks it pending, the thread's next
/// message routes back as the answer. Only an explicit approval confirms.
/// </summary>
public sealed class SpecDialogOutcomeConfirmer(
    IDialogueTransport dialogueTransport,
    SpecDialogQuestionPump questionPump,
    SpecDialogPendingQuestions pendingQuestions,
    SpecDialogOutcomeComposer composer,
    ILogger<SpecDialogOutcomeConfirmer> logger)
{
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(15);
    private static readonly string[] ApprovalAnswers =
        ["yes", "y", "approve", "approved", "ok", "confirm", "confirmed"];

    public async Task<ConfirmationResult> ConfirmAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        var question = new DialogQuestion(
            Guid.NewGuid().ToString("N"), QuestionType.Approval,
            composer.ComposeConfirmation(proposal),
            Context: null, Choices: null, DefaultAnswer: "", ConfirmationTimeout);

        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pump = questionPump.PumpAsync(state, pumpCts.Token);
        try
        {
            await dialogueTransport.PublishQuestionAsync(state.JobId, question, cancellationToken);
            var answer = await dialogueTransport.WaitForAnswerAsync(
                state.JobId, question.QuestionId, ConfirmationTimeout, cancellationToken);
            return Interpret(state, answer);
        }
        finally
        {
            pumpCts.Cancel();
            await pump;
            // A timed-out confirmation must not swallow the thread's next
            // design message as a stale answer.
            pendingQuestions.Clear(state.JobId);
        }
    }

    private ConfirmationResult Interpret(ConversationState state, DialogAnswer? answer)
    {
        if (answer is null)
        {
            logger.LogWarning(
                "Outcome confirmation for spec-dialog session {SessionId} timed out — nothing routes",
                state.JobId);
            return ConfirmationResult.TimedOut;
        }
        return ApprovalAnswers.Contains(answer.Answer.Trim().ToLowerInvariant())
            ? ConfirmationResult.Confirmed
            : ConfirmationResult.Declined;
    }
}
