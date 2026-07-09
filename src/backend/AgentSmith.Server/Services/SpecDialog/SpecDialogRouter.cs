using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The SpecDialog branch of inbound chat routing: /spec commands and follow-up
/// messages inside a thread with an open spec-dialog session are handled here;
/// everything else returns false so normal chat + run-triggers stay untouched.
/// p0315b: a follow-up turn runs the design-partner master (one in-process
/// spec-dialog pipeline run) instead of the p0315a receipt; a turn blocked on
/// an ask_human question consumes the next thread message as its answer.
/// </summary>
public sealed class SpecDialogRouter(
    SpecCommandParser parser,
    SpecDialogSessionManager sessions,
    SpecDialogCommandHandler commandHandler,
    ISpecDialogTurnRunner turnRunner,
    SpecDialogOutcomeFlow outcomeFlow,
    SpecDialogTurnGate turnGate,
    SpecDialogPendingQuestions pendingQuestions,
    IDialogueTransport dialogueTransport,
    SpecDialogReplyComposer composer,
    SpecDialogMessenger messenger,
    ILogger<SpecDialogRouter> logger)
{
    /// <summary>
    /// Routes the message if it belongs to the spec-dialog flow. Returns true
    /// when handled; false hands the message back to the normal intent path.
    /// </summary>
    public async Task<bool> TryRouteAsync(
        string text, string userId, string channelId, string? threadId,
        string platform, CancellationToken ct)
    {
        var command = parser.Parse(text);
        if (command is null)
            return await TryContinueThreadAsync(text, userId, channelId, threadId, platform, ct);

        if (threadId is null)
        {
            logger.LogWarning("/spec received without a thread context on {Platform}, ignoring", platform);
            return false;
        }

        await commandHandler.HandleAsync(command, userId, channelId, threadId, platform, ct);
        return true;
    }

    private async Task<bool> TryContinueThreadAsync(
        string text, string userId, string channelId, string? threadId,
        string platform, CancellationToken ct)
    {
        if (threadId is null) return false;

        var state = await sessions.AppendTurnAsync(platform, threadId, TranscriptRole.User, text, ct);
        if (state is null) return false;

        // A live ask_human question wins: the running master is blocked on it,
        // so this message IS the answer (it stays in the transcript either way).
        if (pendingQuestions.TryTake(state.JobId, out var questionId))
        {
            await dialogueTransport.PublishAnswerAsync(
                state.JobId,
                new DialogAnswer(questionId, text, null, DateTimeOffset.UtcNow, userId), ct);
            return true;
        }

        if (!turnGate.TryEnter(state.JobId))
        {
            await messenger.SendAsync(platform, channelId, threadId, composer.ComposeTurnInProgress(state), ct);
            return true;
        }
        try
        {
            await RunTurnAsync(state, channelId, threadId, platform, ct);
        }
        finally
        {
            turnGate.Exit(state.JobId);
        }
        return true;
    }

    private async Task RunTurnAsync(
        ConversationState state, string channelId, string threadId,
        string platform, CancellationToken ct)
    {
        var current = state;
        while (true)
        {
            SpecDialogTurnResult result;
            try
            {
                result = await turnRunner.RunTurnAsync(current, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Spec-dialog turn failed for session {SessionId}", current.JobId);
                await messenger.SendAsync(
                    platform, channelId, threadId, composer.ComposeTurnFailed(ex.Message), ct);
                return;
            }

            await sessions.AppendTurnAsync(platform, threadId, TranscriptRole.Assistant, result.Reply, ct);
            await messenger.SendAsync(platform, channelId, threadId, result.Reply, ct);
            // p0315e: a non-answer outcome is proposed + confirmed in-thread,
            // then handed to the outcome sink (p0315c: ticket filing). Runs
            // inside the turn gate; the pending-question branch above routes
            // the approval answer.
            var flowResult = await outcomeFlow.HandleAsync(current, result.Outcome, ct);
            if (flowResult is not OutcomeFlowEditRequested edit) return;

            // p0315c edit: the operator's note arrived as a thread message and
            // was already appended to the durable transcript by its own
            // inbound routing; re-load the state so the re-prompted master
            // sees the note as the latest user turn.
            var refreshed = await sessions.GetOpenByThreadAsync(platform, threadId, ct);
            if (refreshed is null)
            {
                logger.LogWarning(
                    "Session {SessionId} closed while an outcome edit was pending — stopping",
                    current.JobId);
                return;
            }
            logger.LogInformation(
                "Re-running design turn for session {SessionId} with the operator's edit note",
                current.JobId);
            current = refreshed;
        }
    }
}
