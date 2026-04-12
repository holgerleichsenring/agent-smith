using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Microsoft Teams implementation of IPlatformAdapter.
/// Uses raw HTTP calls to the Bot Framework REST API with Adaptive Cards.
/// Requires TEAMS_APP_ID, TEAMS_APP_PASSWORD, and optionally TEAMS_TENANT_ID.
/// </summary>
public sealed class TeamsAdapter(
    HttpClient httpClient,
    TeamsAdapterOptions options,
    ILogger<TeamsAdapter> logger) : IPlatformAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Bot Framework token cache
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // Tracks the progress activity ID per conversation so we can update instead of re-post
    private readonly ConcurrentDictionary<string, string> _progressActivityIds = new();

    // Tracks service URL per conversation (Bot Framework requires using the service URL from the original activity)
    private readonly ConcurrentDictionary<string, string> _serviceUrls = new();

    // Pending typed question completions, keyed by questionId
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogAnswer?>>
        _pendingTypedQuestions = new();

    public string Platform => "teams";

    /// <summary>
    /// Registers the service URL for a conversation. Called when receiving activities.
    /// </summary>
    internal void RegisterServiceUrl(string conversationId, string serviceUrl)
    {
        _serviceUrls[conversationId] = serviceUrl.TrimEnd('/');
    }

    public async Task SendMessageAsync(string channelId, string text,
        CancellationToken cancellationToken)
    {
        var activity = new JsonObject
        {
            ["type"] = "message",
            ["text"] = text,
        };

        await SendActivityAsync(channelId, activity, cancellationToken);
    }

    public async Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken)
    {
        var card = TeamsCardBuilder.BuildProgressCard(step, total, commandName);
        var activity = WrapCardInActivity(card);

        if (_progressActivityIds.TryGetValue(channelId, out var existingId))
        {
            var updated = await UpdateActivityAsync(channelId, existingId, activity, cancellationToken);
            if (updated) return;
        }

        var activityId = await SendActivityAsync(channelId, activity, cancellationToken);
        if (activityId is not null)
            _progressActivityIds[channelId] = activityId;
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

        var card = TeamsCardBuilder.BuildQuestionCard(question);
        var activity = WrapCardInActivity(card, $"\ud83d\udcad Question: {question.Text}");
        await SendActivityAsync(channelId, activity, cancellationToken);

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
        var card = TeamsCardBuilder.BuildInfoCard(title, text);
        var activity = WrapCardInActivity(card, $"\u2139\ufe0f {title}: {text}");
        await SendActivityAsync(channelId, activity, cancellationToken);
    }

    public async Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken)
    {
        _progressActivityIds.TryRemove(channelId, out _);

        var card = TeamsCardBuilder.BuildDoneCard(summary, prUrl);
        var activity = WrapCardInActivity(card, $"\u2705 Done! {summary}");
        await SendActivityAsync(channelId, activity, cancellationToken);
    }

    public async Task SendErrorAsync(string channelId, ErrorContext errorContext,
        CancellationToken cancellationToken)
    {
        _progressActivityIds.TryRemove(channelId, out _);

        var card = TeamsCardBuilder.BuildErrorCard(errorContext.FriendlyError, errorContext.LogUrl);
        var activity = WrapCardInActivity(card, $"\u274c Error: {errorContext.FriendlyError}");
        await SendActivityAsync(channelId, activity, cancellationToken);
    }

    public async Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken)
    {
        var card = TeamsCardBuilder.BuildAnsweredCard(questionText, answer);
        var activity = WrapCardInActivity(card);

        await UpdateActivityAsync(channelId, messageId, activity, cancellationToken);
    }

    public async Task SendDetailAsync(string channelId, string text,
        CancellationToken cancellationToken)
    {
        // Teams doesn't have native threading like Slack;
        // post as a reply in the conversation
        var activity = new JsonObject
        {
            ["type"] = "message",
            ["text"] = text,
        };

        await SendActivityAsync(channelId, activity, cancellationToken);
    }

    public async Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken)
    {
        var card = TeamsCardBuilder.BuildClarificationCard(suggestion);
        var activity = WrapCardInActivity(card, $"\ud83e\udd14 Did you mean: {suggestion}?");
        await SendActivityAsync(channelId, activity, cancellationToken);
    }

    /// <summary>
    /// Completes a pending typed question. Called by TeamsInteractionHandler.
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

    internal bool HasPendingTypedQuestion(string questionId) =>
        _pendingTypedQuestions.ContainsKey(questionId);

    // --- Bot Framework REST API ---

    private async Task<string?> SendActivityAsync(
        string conversationId, JsonObject activity,
        CancellationToken cancellationToken)
    {
        var serviceUrl = GetServiceUrl(conversationId);
        var url = $"{serviceUrl}/v3/conversations/{conversationId}/activities";
        var token = await GetTokenAsync(cancellationToken);

        var json = activity.ToJsonString(JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonNode.Parse(body);
            return result?["id"]?.GetValue<string>();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Teams API call failed for conversation {ConversationId}", conversationId);
            return null;
        }
    }

    private async Task<bool> UpdateActivityAsync(
        string conversationId, string activityId, JsonObject activity,
        CancellationToken cancellationToken)
    {
        var serviceUrl = GetServiceUrl(conversationId);
        var url = $"{serviceUrl}/v3/conversations/{conversationId}/activities/{activityId}";
        var token = await GetTokenAsync(cancellationToken);

        activity["id"] = activityId;
        var json = activity.ToJsonString(JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Teams update activity failed for {ActivityId}", activityId);
            return false;
        }
    }

    private string GetServiceUrl(string conversationId)
    {
        if (_serviceUrls.TryGetValue(conversationId, out var url))
            return url;

        return "https://smba.trafficmanager.net/amer";
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            var tokenUrl = "https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.AppId,
                ["client_secret"] = options.AppPassword,
                ["scope"] = "https://api.botframework.com/.default",
            });

            using var response = await httpClient.PostAsync(tokenUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonNode.Parse(body);

            _cachedToken = json?["access_token"]?.GetValue<string>()
                           ?? throw new InvalidOperationException("No access_token in response");
            var expiresIn = json?["expires_in"]?.GetValue<int>() ?? 3600;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60); // Refresh 60s early

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static JsonObject WrapCardInActivity(JsonObject card, string? fallbackText = null)
    {
        return new JsonObject
        {
            ["type"] = "message",
            ["text"] = fallbackText ?? "",
            ["attachments"] = new JsonArray
            {
                new JsonObject
                {
                    ["contentType"] = "application/vnd.microsoft.card.adaptive",
                    ["content"] = card,
                }
            },
        };
    }
}
