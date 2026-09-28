using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
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
    public string Platform => "teams";

    internal void RegisterServiceUrl(string conversationId, string serviceUrl)
        => apiClient.RegisterServiceUrl(conversationId, serviceUrl);

    public Task SendMessageAsync(string channelId, string text, CancellationToken cancellationToken)
        => apiClient.SendActivityAsync(channelId,
            new JsonObject { ["type"] = "message", ["text"] = text }, cancellationToken);

    // Teams threads are addressed by the conversation id itself, so threadId
    // needs no extra routing (see SendInfoAsync).
    public async Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId, DialogQuestion question, string? threadId, CancellationToken cancellationToken)
    {
        if (question.Type == QuestionType.Info)
        {
            await SendInfoAsync(channelId, question.Text, question.Context ?? "", threadId, cancellationToken);
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

    // Teams threads are addressed by the conversation id itself (a channel
    // thread carries a ";messageid=" suffix), so threadId needs no extra routing.
    public Task SendInfoAsync(string channelId, string title, string text,
        string? threadId, CancellationToken cancellationToken)
        => SendCardAsync(channelId, cardBuilder.BuildInfoCard(title, text),
            $"\u2139\ufe0f {title}: {text}", cancellationToken);

    public Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken)
        => apiClient.UpdateActivityAsync(channelId, messageId,
            TeamsApiClient.WrapCardInActivity(cardBuilder.BuildAnsweredCard(questionText, answer)),
            cancellationToken);

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
