using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Shared HTTP client for OpenAI-compatible APIs (OpenAI, Azure OpenAI, Ollama, Groq).
/// Handles chat completions with optional tool calling support.
/// </summary>
public sealed class OpenAiCompatibleClient(
    string endpoint,
    string? apiKey,
    ILogger logger,
    bool useApiKeyHeader = false,
    string? apiVersionQueryParam = null)
{
    private readonly HttpClient _httpClient = CreateHttpClient(endpoint, apiKey, useApiKeyHeader);
    private readonly HttpClient _metadataClient = new();

    public async Task<JsonElement> ChatCompleteAsync(
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

        var path = apiVersionQueryParam is not null
            ? $"chat/completions?api-version={apiVersionQueryParam}"
            : "chat/completions";

        logger.LogDebug("Request URL: {BaseUrl}/{Path}", endpoint.TrimEnd('/'), path);
        logger.LogDebug("Model={Model}, key={KeyPresent}, apiKeyHeader={UseApiKey}",
            model, !string.IsNullOrEmpty(apiKey), useApiKeyHeader);

        var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(path, content, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public async Task<bool> CheckToolCallingSupport(string model, CancellationToken cancellationToken)
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

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
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

    private static HttpClient CreateHttpClient(string endpoint, string? apiKey, bool useApiKeyHeader)
    {
        var client = new HttpClient { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/") };
        if (!string.IsNullOrEmpty(apiKey))
            client.DefaultRequestHeaders.Add(
                useApiKeyHeader ? "api-key" : "Authorization",
                useApiKeyHeader ? apiKey : $"Bearer {apiKey}");
        return client;
    }
}
