using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>Microsoft Teams implementation of IPlatformAdapter.</summary>
public sealed class TeamsAdapter(
    TeamsApiClient apiClient,
    TeamsTypedQuestionTracker questionTracker,
    TeamsCardBuilder cardBuilder,
    ILogger<TeamsAdapter> logger) : IPlatformAdapter
{
    private readonly ConcurrentDictionary<string, string> _progressActivityIds = new();

    public string Platform => "teams";

    internal void RegisterServiceUrl(string conversationId, string serviceUrl)
        => apiClient.RegisterServiceUrl(conversationId, serviceUrl);

    public Task SendMessageAsync(string channelId, string text, CancellationToken cancellationToken)
        => apiClient.SendActivityAsync(channelId,
            new JsonObject { ["type"] = "message", ["text"] = text }, cancellationToken);

    public async Task SendProgressAsync(string channelId, int step, int total,
        string commandName, CancellationToken cancellationToken)
    {
        var activity = TeamsApiClient.WrapCardInActivity(
            cardBuilder.BuildProgressCard(step, total, commandName));

        if (_progressActivityIds.TryGetValue(channelId, out var existingId)
            && await apiClient.UpdateActivityAsync(channelId, existingId, activity, cancellationToken))
            return;

        var activityId = await apiClient.SendActivityAsync(channelId, activity, cancellationToken);
        if (activityId is not null)
            _progressActivityIds[channelId] = activityId;
    }

    public async Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId, DialogQuestion question, CancellationToken cancellationToken)
    {
        if (question.Type == QuestionType.Info)
        {
            await SendInfoAsync(channelId, question.Text, question.Context ?? "", cancellationToken);
            return null;
        }

        await SendCardAsync(channelId, cardBuilder.BuildQuestionCard(question),
            $"\ud83d\udcad Question: {question.Text}", cancellationToken);

        var tcs = questionTracker.Register(question.QuestionId);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(question.Timeout);
            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Typed question {QuestionId} timed out", question.QuestionId);
            return null;
        }
        finally
        {
            questionTracker.Unregister(question.QuestionId);
        }
    }

    public Task SendInfoAsync(string channelId, string title, string text,
        CancellationToken cancellationToken)
        => SendCardAsync(channelId, cardBuilder.BuildInfoCard(title, text),
            $"\u2139\ufe0f {title}: {text}", cancellationToken);

    public Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken)
    {
        _progressActivityIds.TryRemove(channelId, out _);
        return SendCardAsync(channelId, cardBuilder.BuildDoneCard(summary, prUrl),
            $"\u2705 Done! {summary}", cancellationToken);
    }

    public Task SendErrorAsync(string channelId, ErrorContext errorContext,
        CancellationToken cancellationToken)
    {
        _progressActivityIds.TryRemove(channelId, out _);
        return SendCardAsync(channelId,
            cardBuilder.BuildErrorCard(errorContext.FriendlyError, errorContext.LogUrl),
            $"\u274c Error: {errorContext.FriendlyError}", cancellationToken);
    }

    public Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken)
        => apiClient.UpdateActivityAsync(channelId, messageId,
            TeamsApiClient.WrapCardInActivity(cardBuilder.BuildAnsweredCard(questionText, answer)),
            cancellationToken);

    public Task SendDetailAsync(string channelId, string text, CancellationToken cancellationToken)
        => apiClient.SendActivityAsync(channelId,
            new JsonObject { ["type"] = "message", ["text"] = text }, cancellationToken);

    public Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken)
        => SendCardAsync(channelId, cardBuilder.BuildClarificationCard(suggestion),
            $"\ud83e\udd14 Did you mean: {suggestion}?", cancellationToken);

    internal bool TryCompleteTypedQuestion(string questionId, DialogAnswer answer)
        => questionTracker.TryComplete(questionId, answer);

    internal bool HasPendingTypedQuestion(string questionId)
        => questionTracker.HasPending(questionId);

    private Task SendCardAsync(string channelId, JsonObject card, string fallback,
        CancellationToken cancellationToken)
        => apiClient.SendActivityAsync(channelId,
            TeamsApiClient.WrapCardInActivity(card, fallback), cancellationToken);
}
