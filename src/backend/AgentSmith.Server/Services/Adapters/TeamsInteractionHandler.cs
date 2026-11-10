using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Handles Teams Adaptive Card Action.Submit callbacks.
/// Routes question answers to the TeamsAdapter (pending typed questions) or to the run
/// bound to the conversation.
/// </summary>
public sealed class TeamsInteractionHandler(
    ChatRunAnswerRouter answers,
    ClarificationTaker clarifications,
    SlackMessageDispatcher messageDispatcher,
    TeamsAdapter adapter,
    SpecDialogChatClick dialogClicks, // 2026-11-10-ccd9: a submit on a conversation's card, decided by its id
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
        var pending = await clarifications.TakeAsync(DispatcherDefaults.PlatformTeams, conversationId, answer, ct);
        if (pending is not null)
            await messageDispatcher.DispatchAsync(pending.SuggestedText, userId, conversationId, ct,
                conversationId, DispatcherDefaults.PlatformTeams);
    }

    private async Task HandleJobQuestionAsync(
        string conversationId, string userId, string questionId,
        string answer, string? comment, CancellationToken ct)
    {
        // A typed question this replica asked completes its own wait; a conversation's card is decided by its
        // id (2026-11-10-ccd9); a run started from this conversation takes its answer; an older card is told so.
        var dialogAnswer = new DialogAnswer(questionId, answer, comment, DateTimeOffset.UtcNow, userId);
        var teams = DispatcherDefaults.PlatformTeams;
        if (adapter.TryCompleteTypedQuestion(questionId, dialogAnswer)
            || await dialogClicks.TryCurrentAsync(teams, conversationId, conversationId, dialogAnswer, ct)
            || await answers.TryAnswerAsync(new ChatThread(teams, conversationId, conversationId, userId), questionId, answer, comment, ct)
            || await dialogClicks.TryStaleAsync(teams, conversationId, conversationId, dialogAnswer, ct))
        {
            logger.LogInformation("Teams question '{QuestionId}' answered with '{Answer}' by {User}", questionId, answer, userId);
            return;
        }
        logger.LogWarning("Teams answer for {QuestionId} in {ConversationId} matches no open run", questionId, conversationId);
    }
}
