using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Thin HTTP wrapper around the Slack Web API.
/// Handles authentication, serialization, and error logging.
/// </summary>
public sealed class SlackApiClient(
    HttpClient httpClient,
    SlackAdapterOptions options,
    ILogger<SlackApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal async Task<JsonNode?> PostAsync(string method, object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://slack.com/api/{method}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.BotToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Slack API call failed: {Method}", method);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonNode? node = null;
        try
        {
            node = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to parse Slack response for {Method}", method);
            return null;
        }

        var ok = node?["ok"]?.GetValue<bool>() ?? false;
        if (!ok)
        {
            var error = node?["error"]?.GetValue<string>() ?? "unknown";
            logger.LogWarning("Slack API {Method} returned ok=false: {Error}", method, error);
        }

        return node;
    }

    internal static string? ExtractTimestamp(JsonNode? response) =>
        response?["ts"]?.GetValue<string>();
}
