using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Shared HTTP client for OpenAI-compatible APIs (OpenAI, Ollama, etc.).
/// Handles chat completions with optional tool calling support.
/// </summary>
internal sealed class OpenAiCompatibleClient(
    string endpoint,
    string? apiKey,
    ILogger logger)
{
    private readonly HttpClient _httpClient = CreateHttpClient(endpoint, apiKey);

    internal async Task<JsonElement> ChatCompleteAsync(
        string model, JsonArray messages, JsonArray? tools,
        int maxTokens, CancellationToken cancellationToken)
    {
        var request = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["max_tokens"] = maxTokens,
        };

        if (tools is { Count: > 0 })
            request["tools"] = tools;

        logger.LogDebug("Sending chat completion to {Endpoint} with model {Model}", endpoint, model);

        var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json).RootElement;
    }

    internal async Task<bool> CheckToolCallingSupport(string model, CancellationToken cancellationToken)
    {
        try
        {
            var showEndpoint = endpoint.TrimEnd('/').Replace("/v1", "") + "/api/show";
            using var client = new HttpClient();
            var requestBody = new JsonObject { ["name"] = model };
            var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(showEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            // Ollama /api/show returns model info — check template for tool support
            return json.Contains("tool_call", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Tool calling capability check failed, assuming no support");
            return false;
        }
    }

    internal async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var versionEndpoint = endpoint.TrimEnd('/').Replace("/v1", "") + "/api/version";
            using var client = new HttpClient();
            var responseStr = await client.GetStringAsync(versionEndpoint, cancellationToken);
            var responseJson = JsonDocument.Parse(responseStr).RootElement;
            return responseJson.GetProperty("version").GetString() ?? "unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Ollama version from {Endpoint}", endpoint);
            throw;
        }
    }

    private static HttpClient CreateHttpClient(string endpoint, string? apiKey)
    {
        var client = new HttpClient { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/") };
        if (!string.IsNullOrEmpty(apiKey))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        return client;
    }
}
