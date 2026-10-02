using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Handles Slack interactive component callbacks (button clicks): a question's answer goes
/// to the run bound to the thread it was asked in; clarification confirm/help buttons are
/// handled here too.
/// </summary>
public sealed class SlackInteractionHandler(
    ChatRunAnswerRouter answers,
    ClarificationTaker clarifications,
    SlackMessageDispatcher dispatcher,
    SlackAdapter adapter,
    ILogger<SlackInteractionHandler> logger)
{
    private const string ClarificationPrefix = "clarification";

    public async Task HandleAsync(
        string channelId,
        string questionId,
        string answer,
        JsonNode payload,
        CancellationToken cancellationToken)
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
            _ => HandleJobQuestionAsync(channelId, questionId, answer, payload, ct)
        };
    }

    // 2026-10-02-5ab2d: the clicked message loses its buttons on every click, taken or not.
    private async Task HandleClarificationAsync(
        string channelId, string answer, JsonNode payload, CancellationToken ct)
    {
        var pending = await clarifications.TakeAsync(DispatcherDefaults.PlatformSlack, channelId, answer, ct);
        await UpdateClarificationMessageAsync(channelId, answer, pending is not null, payload, ct);
        if (pending is not null)
            await dispatcher.DispatchAsync(pending.SuggestedText, pending.UserId, channelId, ct);
    }

    private async Task HandleJobQuestionAsync(
        string channelId, string questionId, string answer, JsonNode payload, CancellationToken ct)
    {
        // Try to complete a pending typed question first (new AskTypedQuestionAsync flow)
        if (adapter.HasPendingTypedQuestion(questionId))
        {
            var userId = payload["user"]?["id"]?.GetValue<string>()
                         ?? payload["user"]?["username"]?.GetValue<string>()
                         ?? "unknown";

            var dialogAnswer = new DialogAnswer(
                questionId,
                answer,
                Comment: null,
                DateTimeOffset.UtcNow,
                userId);

            adapter.TryCompleteTypedQuestion(questionId, dialogAnswer);
            await UpdateQuestionMessageAsync(channelId, questionId, answer, payload, ct);

            logger.LogInformation("Typed question '{QuestionId}' answered with '{Answer}' by {User}",
                questionId, answer, userId);
            return;
        }

        // A run started from this thread: the binding says which run the answer belongs to.
        var thread = new ChatThread(DispatcherDefaults.PlatformSlack, channelId, ThreadOf(payload),
            payload["user"]?["id"]?.GetValue<string>() ?? "slack-user");
        if (!await answers.TryAnswerAsync(thread, questionId, answer, null, ct))
        {
            logger.LogWarning("Answer for {QuestionId} in {ChannelId} matches no open run", questionId, channelId);
            return;
        }
        await UpdateQuestionMessageAsync(channelId, questionId, answer, payload, ct);
    }

    // A question posted inside a thread carries the thread's ts; one posted at the top has none.
    private static string? ThreadOf(JsonNode payload) =>
        payload["message"]?["thread_ts"]?.GetValue<string>()
        ?? payload["container"]?["thread_ts"]?.GetValue<string>();

    private async Task UpdateClarificationMessageAsync(
        string channelId, string answer, bool taken, JsonNode payload, CancellationToken ct)
    {
        var messageTs = payload["message"]?["ts"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(messageTs)) return;

        var text = answer != ClarificationTaker.Confirm ? "Showing help" : taken ? "Confirmed" : "No longer pending";
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
