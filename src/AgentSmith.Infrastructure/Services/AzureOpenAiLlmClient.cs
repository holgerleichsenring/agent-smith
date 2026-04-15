using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Azure OpenAI ILlmClient that resolves the deployment per task type.
/// Each ModelAssignment can specify its own deployment name.
/// </summary>
public sealed class AzureOpenAiLlmClient(
    string endpoint,
    string apiKey,
    string defaultDeployment,
    string apiVersion,
    IModelRegistry modelRegistry,
    ILogger<AzureOpenAiLlmClient> logger) : ILlmClient
{
    private readonly HttpClient _httpClient = CreateHttpClient(apiKey);

    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt, string userPrompt,
        TaskType taskType, CancellationToken cancellationToken)
    {
        var assignment = modelRegistry.GetModel(taskType);
        var deployment = assignment.Deployment ?? defaultDeployment;

        if (string.IsNullOrEmpty(deployment))
        {
            logger.LogWarning("Azure OpenAI: no deployment for task {TaskType} (model={Model}), skipping",
                taskType, assignment.Model);
            throw new InvalidOperationException(
                $"No deployment configured for task '{taskType}'. " +
                $"Add a 'deployment' field to the '{taskType}' model in the 'models' config section.");
        }

        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}" +
                  $"/chat/completions?api-version={apiVersion}";

        logger.LogDebug("Azure OpenAI request: {Url}", url);
        logger.LogDebug("Azure OpenAI: task={TaskType}, deployment={Deployment}, model={Model}, key={KeyPresent}",
            taskType, deployment, assignment.Model, !string.IsNullOrEmpty(apiKey));

        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt }
            },
            ["max_tokens"] = assignment.MaxTokens,
        };

        var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var text = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()?.Trim() ?? "";

        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
        }

        logger.LogDebug("Azure OpenAI response: {Chars} chars, {In}+{Out} tokens",
            text.Length, inputTokens, outputTokens);
        return new LlmResponse(text, inputTokens, outputTokens, assignment.Model);
    }

    private static HttpClient CreateHttpClient(string apiKey)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("api-key", apiKey);
        return client;
    }
}
