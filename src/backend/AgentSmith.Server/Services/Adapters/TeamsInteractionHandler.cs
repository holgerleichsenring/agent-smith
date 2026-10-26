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
    SpecDialogButtonAnswer dialogButtons, // 2026-10-26-0f19b: a card submit for a question another replica asked
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
        // A typed question this replica asked completes its own wait; one another replica asked for a
        // spec dialog is found through the conversation's session (2026-10-26-0f19b).
        var dialogAnswer = new DialogAnswer(questionId, answer, comment, DateTimeOffset.UtcNow, userId);
        if (adapter.TryCompleteTypedQuestion(questionId, dialogAnswer)
            || await dialogButtons.TryAnswerAsync(DispatcherDefaults.PlatformTeams, conversationId, dialogAnswer, ct))
        {
            logger.LogInformation("Teams typed question '{QuestionId}' answered with '{Answer}' by {User}",
                questionId, answer, userId);
            return;
        }

        // A run started from this conversation: the binding says which run the answer belongs to.
        var thread = new ChatThread(DispatcherDefaults.PlatformTeams, conversationId, conversationId, userId);
        if (!await answers.TryAnswerAsync(thread, questionId, answer, comment, ct))
            logger.LogWarning("Teams answer for {QuestionId} in {ConversationId} matches no open run",
                questionId, conversationId);
    }
}
