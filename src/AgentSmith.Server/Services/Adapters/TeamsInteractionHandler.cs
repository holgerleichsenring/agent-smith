using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Handles Teams Adaptive Card Action.Submit callbacks.
/// Routes question answers to the TeamsAdapter (pending typed questions)
/// or via Redis message bus (legacy flow).
/// </summary>
public sealed class TeamsInteractionHandler(
    IMessageBus messageBus,
    ConversationStateManager stateManager,
    ClarificationStateManager clarificationState,
    SlackMessageDispatcher messageDispatcher,
    HelpHandler helpHandler,
    TeamsAdapter adapter,
    ILogger<TeamsInteractionHandler> logger)
{
    private const string ClarificationPrefix = "clarification";

    public async Task HandleAsync(
        string conversationId,
        string userId,
        JsonNode activityValue,
        CancellationToken cancellationToken)
    {
        try
        {
            var questionId = activityValue["questionId"]?.GetValue<string>();
            var answer = activityValue["answer"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(answer))
            {
                logger.LogWarning("Teams interaction missing questionId or answer");
                return;
            }

            // Handle freetext: the actual answer is in the "freetext" input field
            if (answer == "__freetext__")
            {
                answer = activityValue["freetext"]?.GetValue<string>() ?? "";
            }

            // Handle approval comment
            var comment = activityValue["comment"]?.GetValue<string>();

            await RouteInteractionAsync(conversationId, userId, questionId, answer, comment, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Teams interaction for conversation {ConversationId}", conversationId);
        }
    }

    private Task RouteInteractionAsync(
        string conversationId, string userId, string questionId,
        string answer, string? comment, CancellationToken ct)
    {
        return questionId switch
        {
            ClarificationPrefix => HandleClarificationAsync(conversationId, userId, answer, ct),
            _ => HandleJobQuestionAsync(conversationId, userId, questionId, answer, comment, ct)
        };
    }

    private async Task HandleClarificationAsync(
        string conversationId, string userId, string answer, CancellationToken ct)
    {
        var pending = await clarificationState.GetAsync("teams", conversationId, ct);
        if (pending is null)
        {
            logger.LogWarning("Teams clarification clicked but no pending state for {ConversationId}", conversationId);
            return;
        }

        await clarificationState.ClearAsync("teams", conversationId, ct);

        if (answer == "confirm")
            await messageDispatcher.DispatchAsync(pending.SuggestedText, userId, conversationId, ct);
        else
            await helpHandler.SendHelpAsync(conversationId, ct);
    }

    private async Task HandleJobQuestionAsync(
        string conversationId, string userId, string questionId,
        string answer, string? comment, CancellationToken ct)
    {
        // Try to complete a pending typed question first
        if (adapter.HasPendingTypedQuestion(questionId))
        {
            var dialogAnswer = new DialogAnswer(
                questionId,
                answer,
                comment,
                DateTimeOffset.UtcNow,
                userId);

            adapter.TryCompleteTypedQuestion(questionId, dialogAnswer);

            logger.LogInformation("Teams typed question '{QuestionId}' answered with '{Answer}' by {User}",
                questionId, answer, userId);
            return;
        }

        // Legacy flow: route via message bus
        var state = await stateManager.GetAsync("teams", conversationId, ct);
        if (state is null)
        {
            logger.LogWarning("Teams interaction for {ConversationId} but no active job found", conversationId);
            return;
        }

        if (state.PendingQuestionId != questionId)
        {
            logger.LogWarning("Teams answer for {QuestionId} but pending is {Pending}",
                questionId, state.PendingQuestionId);
            return;
        }

        await messageBus.PublishAnswerAsync(state.JobId, questionId, answer, ct);
        await stateManager.ClearPendingQuestionAsync("teams", conversationId, ct);

        logger.LogInformation("Teams answer '{Answer}' for '{QuestionId}' forwarded to {JobId}",
            answer, questionId, state.JobId);
    }
}
