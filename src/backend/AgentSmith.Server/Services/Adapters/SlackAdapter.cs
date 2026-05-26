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
    SlackProgressFormatter progressFormatter,
    ILogger<SlackAdapter> logger) : IPlatformAdapter
{
    private readonly SlackProgressTracker _progress = new();
    private readonly SlackTypedQuestionManager _typedQuestions = new(logger);

    public string Platform => "slack";

    public async Task SendMessageAsync(string channelId, string text, CancellationToken ct) =>
        await api.PostAsync("chat.postMessage", new { channel = channelId, text }, ct);

    public async Task SendProgressAsync(string channelId, int step, int total,
        string commandName, CancellationToken ct)
    {
        var text = progressFormatter.FormatProgress(step, total, commandName);
        var existingTs = _progress.GetThreadTs(channelId);

        if (existingTs is not null)
        {
            var resp = await api.PostAsync("chat.update",
                new { channel = channelId, ts = existingTs, text }, ct);
            if (resp?["ok"]?.GetValue<bool>() ?? false) return;
        }

        var response = await api.PostAsync("chat.postMessage",
            new { channel = channelId, text }, ct);
        var ts = SlackApiClient.ExtractTimestamp(response);
        if (ts is not null) _progress.SetThreadTs(channelId, ts);
    }

    public async Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken ct)
    {
        _progress.Remove(channelId);
        var text = string.IsNullOrWhiteSpace(prUrl)
            ? $":rocket: *Done!* {summary}"
            : $":rocket: *Done!* {summary}\n:link: <{prUrl}|View Pull Request>";
        await api.PostAsync("chat.postMessage", new { channel = channelId, text }, ct);
    }

    public async Task SendErrorAsync(string channelId, ErrorContext errorContext, CancellationToken ct)
    {
        _progress.Remove(channelId);
        var (fallbackText, blocks) = SlackErrorBlockBuilder.Build(errorContext);
        await api.PostAsync("chat.postMessage",
            new { channel = channelId, text = fallbackText, blocks }, ct);
    }

    public async Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken ct)
    {
        var (text, blocks) = messageBlockBuilder.BuildQuestionAnswered(questionText, answer);
        await api.PostAsync("chat.update",
            new { channel = channelId, ts = messageId, text, blocks }, ct);
    }

    public async Task SendDetailAsync(string channelId, string text, CancellationToken ct)
    {
        var threadTs = _progress.GetThreadTs(channelId);
        if (threadTs is null)
        {
            logger.LogDebug("No progress message to thread detail under for {Channel}", channelId);
            return;
        }
        await api.PostAsync("chat.postMessage",
            new { channel = channelId, text, thread_ts = threadTs }, ct);
    }

    public async Task SendClarificationAsync(string channelId, string suggestion, CancellationToken ct)
    {
        var (text, blocks) = messageBlockBuilder.BuildClarification(suggestion);
        await api.PostAsync("chat.postMessage", new { channel = channelId, text, blocks }, ct);
    }
    public async Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId, DialogQuestion question, CancellationToken ct)
    {
        if (question.Type == QuestionType.Info)
        {
            await SendInfoAsync(channelId, question.Text, question.Context ?? "", ct);
            return null;
        }

        var blocks = typedQuestionBlockBuilder.Build(question);
        var fallback = $":thought_balloon: *Question:* {question.Text}";
        await api.PostAsync("chat.postMessage",
            new { channel = channelId, text = fallback, blocks }, ct);
        return await _typedQuestions.WaitAsync(question.QuestionId, question, ct);
    }

    public async Task SendInfoAsync(string channelId, string title, string text, CancellationToken ct)
    {
        var (fallback, blocks) = messageBlockBuilder.BuildInfo(title, text);
        await api.PostAsync("chat.postMessage",
            new { channel = channelId, text = fallback, blocks }, ct);
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
