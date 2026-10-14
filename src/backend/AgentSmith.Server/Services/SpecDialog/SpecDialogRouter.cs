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
    SpecDialogCommandHandler commandHandler,
    ISpecDialogTurnRunner turnRunner,
    SpecDialogOutcomeFlow outcomeFlow,
    SpecDialogTurnGate turnGate,
    SpecDialogAnswerAdmission admission,
    SpecDialogTurnKeeper keeper,
    SpecDialogEditReload edits,
    SpecDialogReplyComposer composer,
    SpecDialogMessenger messenger,
    ILogger<SpecDialogRouter> logger)
{
    /// <summary>
    /// Routes the message if it belongs to the spec-dialog flow. Returns true when handled; false hands
    /// the message back to the normal intent path. The poster's display name is recorded on its turn.
    /// </summary>
    public async Task<bool> TryRouteAsync(
        string text, string userId, string channelId, string? threadId,
        string platform, bool mayStartRuns, CancellationToken ct, string? author = null)
    {
        var command = parser.Parse(text);
        if (command is null)
            return await TryContinueThreadAsync(text, userId, channelId, threadId, platform, mayStartRuns, author, ct);

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
        string platform, bool mayStartRuns, string? author, CancellationToken ct)
    {
        if (threadId is null) return false;

        // A live question wins: the running master is blocked on it, so this message IS the
        // answer (it stays in the transcript either way).
        var admitted = await admission.AdmitAsync(text, userId, platform, threadId, ct, author);
        if (admitted is null) return false;
        if (admitted.Answered) return true;
        var state = admitted.State;

        if (!turnGate.TryEnter(state.JobId))
        {
            await messenger.SendAsync(platform, channelId, threadId, composer.ComposeTurnInProgress(state), ct);
            return true;
        }
        try
        {
            await RunTurnAsync(state, channelId, threadId, platform, new TurnPoster(userId, mayStartRuns), author, ct);
        }
        finally
        {
            turnGate.Exit(state.JobId);
        }
        // Outside the gate: a message typed while the digest is written starts its own turn.
        await keeper.FoldAsync(state, ct);
        return true;
    }

    private async Task RunTurnAsync(
        ConversationState state, string channelId, string threadId,
        string platform, TurnPoster poster, string? author, CancellationToken ct)
    {
        var current = state;
        while (true)
        {
            SpecDialogTurnResult result;
            try
            {
                result = await turnRunner.RunTurnAsync(current, poster, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Spec-dialog turn failed for session {SessionId}", current.JobId);
                await messenger.SendAsync(
                    platform, channelId, threadId, composer.ComposeTurnFailed(ex.Message), ct);
                return;
            }

            await keeper.KeepAsync(current, result, platform, threadId, ct);
            await messenger.SendAsync(platform, channelId, threadId, result.Shown, ct);
            // p0315e: a non-answer outcome is proposed + confirmed in-thread,
            // then handed to the outcome sink (p0315c: ticket filing). Runs
            // inside the turn gate; the pending-question branch above routes
            // the approval answer.
            var flowResult = await outcomeFlow.HandleAsync(current, result.Outcome, poster.MayStartRuns, ct, result.Stewarded);
            if (flowResult is not OutcomeFlowEditRequested edit) return;
            // p0315c edit: the turn re-runs on a re-read state. 2026-09-22-355b: the note travels
            // as a VALUE — a shape clicked on a chat surface never passed the place that appends.
            if (await edits.RefreshedAsync(current, result.Outcome, edit.Note, ct, author)
                is not { } refreshed) return;
            current = refreshed;
        }
    }
}
