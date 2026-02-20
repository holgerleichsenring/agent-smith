using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace AgentSmith.Dispatcher.Adapters;

/// <summary>
/// Handles Slack interactive component callbacks (button clicks).
/// Routes user answers from yes/no question buttons back to the waiting agent
/// via Redis Streams, and updates the original Slack message to reflect the choice.
/// </summary>
public sealed class SlackInteractionHandler(
    IMessageBus messageBus,
    ConversationStateManager stateManager,
    SlackAdapter adapter,
    ILogger<SlackInteractionHandler> logger)
{
    public async Task HandleAsync(
        string channelId,
        string questionId,
        string answer,
        JsonNode payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await stateManager.GetAsync(DispatcherDefaults.PlatformSlack, channelId, cancellationToken);
            if (state is null)
            {
                logger.LogWarning(
                    "Received interaction for channel {ChannelId} but no active job found", channelId);
                return;
            }

            if (!IsExpectedQuestion(state, questionId))
                return;

            await messageBus.PublishAnswerAsync(state.JobId, questionId, answer, cancellationToken);
            await UpdateQuestionMessageAsync(channelId, questionId, answer, payload, cancellationToken);
            await stateManager.ClearPendingQuestionAsync(DispatcherDefaults.PlatformSlack, channelId, cancellationToken);

            logger.LogInformation(
                "Answer '{Answer}' for question '{QuestionId}' forwarded to job {JobId}",
                answer, questionId, state.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Slack interaction for channel {ChannelId}", channelId);
        }
    }

    private bool IsExpectedQuestion(ConversationState state, string questionId)
    {
        if (state.PendingQuestionId == questionId) return true;

        logger.LogWarning(
            "Received answer for question {QuestionId} but pending question is {Pending}",
            questionId, state.PendingQuestionId);
        return false;
    }

    private async Task UpdateQuestionMessageAsync(
        string channelId,
        string questionId,
        string answer,
        JsonNode payload,
        CancellationToken cancellationToken)
    {
        var messageTs = payload["message"]?["ts"]?.GetValue<string>() ?? string.Empty;
        var questionText = payload["actions"]?[0]?["block_id"]?.GetValue<string>() ?? questionId;

        if (string.IsNullOrWhiteSpace(messageTs)) return;

        await adapter.UpdateQuestionAnsweredAsync(channelId, messageTs, questionText, answer, cancellationToken);
    }
}
