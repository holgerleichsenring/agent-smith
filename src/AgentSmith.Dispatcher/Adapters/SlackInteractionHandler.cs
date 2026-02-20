using AgentSmith.Dispatcher.Handlers;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace AgentSmith.Dispatcher.Adapters;

/// <summary>
/// Handles Slack interactive component callbacks (button clicks).
/// Routes job question answers via Redis Streams, and handles
/// clarification confirm/help buttons.
/// </summary>
public sealed class SlackInteractionHandler(
    IMessageBus messageBus,
    ConversationStateManager stateManager,
    ClarificationStateManager clarificationState,
    SlackMessageDispatcher dispatcher,
    SlackErrorActionHandler errorActionHandler,
    HelpHandler helpHandler,
    SlackAdapter adapter,
    ILogger<SlackInteractionHandler> logger)
{
    private const string ClarificationPrefix = "clarification";
    private const string ErrorPrefix = "error";

    public async Task HandleAsync(
        string channelId,
        string questionId,
        string answer,
        JsonNode payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await RouteInteractionAsync(channelId, questionId, answer, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling interaction for channel {ChannelId}", channelId);
        }
    }

    private Task RouteInteractionAsync(
        string channelId, string questionId, string answer,
        JsonNode payload, CancellationToken ct)
    {
        return questionId switch
        {
            ClarificationPrefix => HandleClarificationAsync(channelId, answer, payload, ct),
            ErrorPrefix => errorActionHandler.HandleAsync(channelId, answer, payload, ct),
            _ => HandleJobQuestionAsync(channelId, questionId, answer, payload, ct)
        };
    }

    private async Task HandleClarificationAsync(
        string channelId, string answer, JsonNode payload, CancellationToken ct)
    {
        var pending = await clarificationState.GetAsync(DispatcherDefaults.PlatformSlack, channelId, ct);
        if (pending is null)
        {
            logger.LogWarning("Clarification clicked but no pending state for {ChannelId}", channelId);
            return;
        }

        await clarificationState.ClearAsync(DispatcherDefaults.PlatformSlack, channelId, ct);
        await UpdateClarificationMessageAsync(channelId, answer, payload, ct);

        if (answer == "confirm")
            await dispatcher.DispatchAsync(pending.SuggestedText, pending.UserId, channelId, ct);
        else
            await helpHandler.SendHelpAsync(channelId, ct);
    }

    private async Task HandleJobQuestionAsync(
        string channelId, string questionId, string answer, JsonNode payload, CancellationToken ct)
    {
        var state = await stateManager.GetAsync(DispatcherDefaults.PlatformSlack, channelId, ct);
        if (state is null)
        {
            logger.LogWarning("Interaction for {ChannelId} but no active job found", channelId);
            return;
        }

        if (!IsExpectedQuestion(state, questionId)) return;

        await messageBus.PublishAnswerAsync(state.JobId, questionId, answer, ct);
        await UpdateQuestionMessageAsync(channelId, questionId, answer, payload, ct);
        await stateManager.ClearPendingQuestionAsync(DispatcherDefaults.PlatformSlack, channelId, ct);

        logger.LogInformation("Answer '{Answer}' for '{QuestionId}' forwarded to {JobId}",
            answer, questionId, state.JobId);
    }

    private bool IsExpectedQuestion(ConversationState state, string questionId)
    {
        if (state.PendingQuestionId == questionId) return true;

        logger.LogWarning("Answer for {QuestionId} but pending is {Pending}",
            questionId, state.PendingQuestionId);
        return false;
    }

    private async Task UpdateClarificationMessageAsync(
        string channelId, string answer, JsonNode payload, CancellationToken ct)
    {
        var messageTs = payload["message"]?["ts"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(messageTs)) return;

        var text = answer == "confirm" ? "Confirmed" : "Showing help";
        await adapter.UpdateQuestionAnsweredAsync(channelId, messageTs, "Clarification", text, ct);
    }

    private async Task UpdateQuestionMessageAsync(
        string channelId, string questionId, string answer, JsonNode payload, CancellationToken ct)
    {
        var messageTs = payload["message"]?["ts"]?.GetValue<string>() ?? string.Empty;
        var questionText = payload["actions"]?[0]?["block_id"]?.GetValue<string>() ?? questionId;
        if (string.IsNullOrWhiteSpace(messageTs)) return;

        await adapter.UpdateQuestionAnsweredAsync(channelId, messageTs, questionText, answer, ct);
    }
}
