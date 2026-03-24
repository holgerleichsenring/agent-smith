using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// OpenAI-compatible ILlmClient for bootstrap tasks (context.yaml, code-map, coding-principles).
/// Works with OpenAI, Groq, Together AI, and any OpenAI-compatible endpoint.
/// </summary>
public sealed class OpenAiLlmClient(
    OpenAiCompatibleClient client,
    string model,
    IModelRegistry modelRegistry,
    ILogger<OpenAiLlmClient> logger) : ILlmClient
{
    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        TaskType taskType,
        CancellationToken cancellationToken)
    {
        var assignment = modelRegistry.GetModel(taskType);

        logger.LogDebug("LLM call: task={TaskType}, model={Model}, maxTokens={MaxTokens}",
            taskType, assignment.Model, assignment.MaxTokens);

        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user", ["content"] = userPrompt }
        };

        var response = await client.ChatCompleteAsync(
            assignment.Model, messages, null, assignment.MaxTokens, cancellationToken);

        var text = response.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()?.Trim() ?? "";

        logger.LogDebug("LLM response: {Chars} chars", text.Length);
        return text;
    }
}
