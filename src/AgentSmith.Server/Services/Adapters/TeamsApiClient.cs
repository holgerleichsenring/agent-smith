using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Handles Bot Framework REST API communication for sending
/// and updating activities in Teams conversations.
/// </summary>
public sealed class TeamsApiClient(
    HttpClient httpClient,
    BotFrameworkTokenProvider tokenProvider,
    ILogger<TeamsApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, string> _serviceUrls = new();

    internal void RegisterServiceUrl(string conversationId, string serviceUrl)
        => _serviceUrls[conversationId] = serviceUrl.TrimEnd('/');

    internal async Task<string?> SendActivityAsync(
        string conversationId, JsonObject activity, CancellationToken cancellationToken)
    {
        var url = $"{GetServiceUrl(conversationId)}/v3/conversations/{conversationId}/activities";
        var token = await tokenProvider.GetTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(activity.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonNode.Parse(body)?["id"]?.GetValue<string>();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Teams API call failed for conversation {ConversationId}", conversationId);
            return null;
        }
    }

    internal async Task<bool> UpdateActivityAsync(
        string conversationId, string activityId, JsonObject activity,
        CancellationToken cancellationToken)
    {
        var url = $"{GetServiceUrl(conversationId)}/v3/conversations/{conversationId}/activities/{activityId}";
        var token = await tokenProvider.GetTokenAsync(cancellationToken);

        activity["id"] = activityId;
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(activity.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");

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

    internal static JsonObject WrapCardInActivity(JsonObject card, string? fallbackText = null)
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

    private string GetServiceUrl(string conversationId)
    {
        if (_serviceUrls.TryGetValue(conversationId, out var url))
            return url;
        return "https://smba.trafficmanager.net/amer";
    }
}
