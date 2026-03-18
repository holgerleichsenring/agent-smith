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
    private readonly HttpClient _metadataClient = new();

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
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    internal async Task<bool> CheckToolCallingSupport(string model, CancellationToken cancellationToken)
    {
        try
        {
            var showUrl = BaseMetadataUrl() + "/api/show";
            var requestBody = new JsonObject { ["name"] = model };
            var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await _metadataClient.PostAsync(showUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
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
            var versionUrl = BaseMetadataUrl() + "/api/version";
            var responseStr = await _metadataClient.GetStringAsync(versionUrl, cancellationToken);
            using var doc = JsonDocument.Parse(responseStr);
            return doc.RootElement.GetProperty("version").GetString() ?? "unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Ollama version from {Endpoint}", endpoint);
            throw;
        }
    }

    private string BaseMetadataUrl() => endpoint.TrimEnd('/').Replace("/v1", "");

    private static HttpClient CreateHttpClient(string endpoint, string? apiKey)
    {
        var client = new HttpClient { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/") };
        if (!string.IsNullOrEmpty(apiKey))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        return client;
    }
}
