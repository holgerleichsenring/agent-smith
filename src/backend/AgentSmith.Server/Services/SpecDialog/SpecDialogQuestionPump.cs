using AgentSmith.Infrastructure.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: relays a running design turn's ask_human questions into its chat
/// thread. The master publishes questions on job:{sessionId}:out via
/// IDialogueTransport; this pump subscribes for the turn's duration, posts
/// each question threaded, and marks it pending so the thread's next message
/// routes back as the answer. A dead question bridge (no Redis in tests)
/// degrades to a logged warning — the turn itself still runs.
/// <para>
/// 2026-11-04-df50a: the stream is the CONVERSATION's and outlives every turn, so a relay read
/// from its start re-posted every earlier question and set it pending again — the operator's
/// next message then went to a wait nobody held and was lost. The turn's starting position is
/// taken before its pipeline runs, and only what follows it is relayed.
/// </para>
/// </summary>
public sealed class SpecDialogQuestionPump(
    IMessageBus messageBus,
    SpecDialogMessenger messenger,
    SpecDialogPendingQuestions pendingQuestions,
    SpecDialogReplyComposer composer,
    ILogger<SpecDialogQuestionPump> logger)
{
    /// <summary>
    /// Takes the stream's current position, then starts relaying what is published after it.
    /// Await this BEFORE the turn's pipeline runs; the returned task is the running relay.
    /// </summary>
    public async Task<Task> StartAsync(ConversationState state, CancellationToken cancellationToken)
    {
        try
        {
            var after = await messageBus.TailAsync(state.JobId, cancellationToken);
            return PumpAsync(state, after, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Question bridge unavailable for spec-dialog session {SessionId} — "
                + "ask_human answers cannot reach this turn", state.JobId);
            return Task.CompletedTask;
        }
    }

    private async Task PumpAsync(ConversationState state, string after, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in messageBus.SubscribeToJobAsync(state.JobId, after, cancellationToken))
            {
                if (message.Type != BusMessageType.Question
                    || string.IsNullOrEmpty(message.QuestionId)) continue;
                pendingQuestions.Set(state.JobId, message.QuestionId!, message.Text);
                await messenger.SendAsync(
                    state.Platform, state.ChannelId, state.ThreadId!,
                    composer.ComposeQuestion(message.Text), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Question pump for session {SessionId} stopped (turn ended)", state.JobId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Question bridge unavailable for spec-dialog session {SessionId} — "
                + "ask_human answers cannot reach this turn", state.JobId);
        }
    }
}
