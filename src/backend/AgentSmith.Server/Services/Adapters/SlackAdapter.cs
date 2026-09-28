using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using System.Text.Json.Nodes;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

public sealed class SlackAdapter(
    SlackApiClient api,
    SlackTypedQuestionBlockBuilder typedQuestionBlockBuilder,
    SlackMessageBlockBuilder messageBlockBuilder,
    ILogger<SlackAdapter> logger) : IPlatformAdapter
{
    private readonly SlackTypedQuestionManager _typedQuestions = new(logger);

    public string Platform => "slack";

    public async Task SendMessageAsync(string channelId, string text, CancellationToken ct) =>
        await api.PostAsync("chat.postMessage", new { channel = channelId, text }, ct);

    public async Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken ct)
    {
        var (text, blocks) = messageBlockBuilder.BuildQuestionAnswered(questionText, answer);
        await api.PostAsync("chat.update",
            new { channel = channelId, ts = messageId, text, blocks }, ct);
    }

    public async Task SendClarificationAsync(string channelId, string suggestion, CancellationToken ct)
    {
        var (text, blocks) = messageBlockBuilder.BuildClarification(suggestion);
        await api.PostAsync("chat.postMessage", new { channel = channelId, text, blocks }, ct);
    }
    public async Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId, DialogQuestion question, string? threadId, CancellationToken ct)
    {
        if (question.Type == QuestionType.Info)
        {
            await SendInfoAsync(channelId, question.Text, question.Context ?? "", threadId, ct);
            return null;
        }

        var blocks = typedQuestionBlockBuilder.Build(question);
        var fallback = $":thought_balloon: *Question:* {question.Text}";
        await api.PostAsync("chat.postMessage",
            new { channel = channelId, text = fallback, blocks, thread_ts = threadId }, ct);
        return await _typedQuestions.WaitAsync(question.QuestionId, question, ct);
    }

    public async Task SendInfoAsync(string channelId, string title, string text,
        string? threadId, CancellationToken ct)
    {
        var (fallback, blocks) = messageBlockBuilder.BuildInfo(title, text);
        await api.PostAsync("chat.postMessage",
            new { channel = channelId, text = fallback, blocks, thread_ts = threadId }, ct);
    }
    internal bool TryCompleteTypedQuestion(string questionId, DialogAnswer answer) =>
        _typedQuestions.TryComplete(questionId, answer);

    internal bool HasPendingTypedQuestion(string questionId) =>
        _typedQuestions.HasPending(questionId);
    internal async Task<JsonNode?> OpenViewAsync(
        string triggerId, object view, CancellationToken ct) =>
        await api.PostAsync("views.open", new { trigger_id = triggerId, view }, ct);

    internal async Task<JsonNode?> UpdateViewAsync(
        string viewId, object view, CancellationToken ct) =>
        await api.PostAsync("views.update", new { view_id = viewId, view }, ct);
}
