using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Slack Web API implementation of IPlatformAdapter.
/// Delegates HTTP calls to <see cref="SlackApiClient"/>.
/// Requires SLACK_BOT_TOKEN and SLACK_SIGNING_SECRET environment variables.
/// </summary>
public sealed class SlackAdapter(
    SlackApiClient api,
    SlackTypedQuestionBlockBuilder typedQuestionBlockBuilder,
    SlackMessageBlockBuilder messageBlockBuilder,
    SlackProgressFormatter progressFormatter,
    ILogger<SlackAdapter> logger) : IPlatformAdapter
{

    // Tracks the progress message ts per channel so we can update instead of re-post
    private readonly ConcurrentDictionary<string, string>
        _progressMessageTs = new();

    // Pending typed question completions, keyed by questionId
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogAnswer?>>
        _pendingTypedQuestions = new();

    public string Platform => "slack";

    public async Task SendMessageAsync(string channelId, string text,
        CancellationToken cancellationToken)
    {
        var payload = new { channel = channelId, text };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken)
    {
        var text = progressFormatter.FormatProgress(step, total, commandName);

        if (_progressMessageTs.TryGetValue(channelId, out var existingTs))
        {
            // Update the existing progress message instead of flooding the channel
            var updatePayload = new { channel = channelId, ts = existingTs, text };
            var updateResponse = await api.PostAsync("chat.update", updatePayload, cancellationToken);

            // If update fails for any reason, fall back to a new message
            var ok = updateResponse?["ok"]?.GetValue<bool>() ?? false;
            if (ok) return;
        }

        // First step or fallback: post a new message and remember its ts
        var postPayload = new { channel = channelId, text };
        var response = await api.PostAsync("chat.postMessage", postPayload, cancellationToken);
        var ts = SlackApiClient.ExtractTimestamp(response);
        if (ts is not null)
            _progressMessageTs[channelId] = ts;
    }

    public async Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken)
    {
        // Remove progress message tracking for this channel
        _progressMessageTs.TryRemove(channelId, out _);

        var text = string.IsNullOrWhiteSpace(prUrl)
            ? $":rocket: *Done!* {summary}"
            : $":rocket: *Done!* {summary}\n:link: <{prUrl}|View Pull Request>";

        var payload = new { channel = channelId, text };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendErrorAsync(string channelId, ErrorContext errorContext,
        CancellationToken cancellationToken)
    {
        _progressMessageTs.TryRemove(channelId, out _);

        var (fallbackText, blocks) = SlackErrorBlockBuilder.Build(errorContext);
        var payload = new { channel = channelId, text = fallbackText, blocks };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken)
    {
        var (text, blocks) = messageBlockBuilder.BuildQuestionAnswered(questionText, answer);
        var payload = new { channel = channelId, ts = messageId, text, blocks };
        await api.PostAsync("chat.update", payload, cancellationToken);
    }

    public async Task SendDetailAsync(string channelId, string text,
        CancellationToken cancellationToken)
    {
        if (!_progressMessageTs.TryGetValue(channelId, out var threadTs))
        {
            logger.LogDebug("No progress message to thread detail under for {Channel}", channelId);
            return;
        }

        var payload = new { channel = channelId, text, thread_ts = threadTs };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken)
    {
        var (text, blocks) = messageBlockBuilder.BuildClarification(suggestion);
        var payload = new { channel = channelId, text, blocks };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId,
        DialogQuestion question,
        CancellationToken cancellationToken)
    {
        if (question.Type == QuestionType.Info)
        {
            await SendInfoAsync(channelId, question.Text, question.Context ?? "", cancellationToken);
            return null;
        }

        var blocks = typedQuestionBlockBuilder.Build(question);
        var fallbackText = $":thought_balloon: *Question:* {question.Text}";

        var payload = new { channel = channelId, text = fallbackText, blocks };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);

        // Register a TCS and wait for the interaction handler to complete it
        var tcs = new TaskCompletionSource<DialogAnswer?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTypedQuestions[question.QuestionId] = tcs;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(question.Timeout);

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Typed question {QuestionId} timed out after {Timeout}",
                question.QuestionId, question.Timeout);
            return null;
        }
        finally
        {
            _pendingTypedQuestions.TryRemove(question.QuestionId, out _);
        }
    }

    public async Task SendInfoAsync(string channelId, string title, string text,
        CancellationToken cancellationToken)
    {
        var (fallback, blocks) = messageBlockBuilder.BuildInfo(title, text);
        var payload = new { channel = channelId, text = fallback, blocks };
        await api.PostAsync("chat.postMessage", payload, cancellationToken);
    }

    /// <summary>
    /// Completes a pending typed question with the given answer.
    /// Called by <see cref="SlackInteractionHandler"/> when a button click arrives.
    /// </summary>
    internal bool TryCompleteTypedQuestion(string questionId, DialogAnswer answer)
    {
        if (_pendingTypedQuestions.TryRemove(questionId, out var tcs))
        {
            tcs.TrySetResult(answer);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a typed question is currently pending for the given questionId.
    /// </summary>
    internal bool HasPendingTypedQuestion(string questionId) =>
        _pendingTypedQuestions.ContainsKey(questionId);

    // --- Modal helpers ---

    internal async Task<JsonNode?> OpenViewAsync(
        string triggerId, object view, CancellationToken ct)
    {
        var payload = new { trigger_id = triggerId, view };
        return await api.PostAsync("views.open", payload, ct);
    }

    internal async Task<JsonNode?> UpdateViewAsync(
        string viewId, object view, CancellationToken ct)
    {
        var payload = new { view_id = viewId, view };
        return await api.PostAsync("views.update", payload, ct);
    }

}
