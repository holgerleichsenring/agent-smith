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
/// </summary>
public sealed class SpecDialogQuestionPump(
    IMessageBus messageBus,
    SpecDialogMessenger messenger,
    SpecDialogPendingQuestions pendingQuestions,
    SpecDialogReplyComposer composer,
    ILogger<SpecDialogQuestionPump> logger)
{
    public async Task PumpAsync(ConversationState state, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in messageBus.SubscribeToJobAsync(state.JobId, cancellationToken))
            {
                if (message.Type != BusMessageType.Question
                    || string.IsNullOrEmpty(message.QuestionId)) continue;
                pendingQuestions.Set(state.JobId, message.QuestionId!);
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
