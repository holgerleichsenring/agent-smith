using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// OpenAI-compatible ILlmClient for bootstrap tasks and pipeline LLM calls.
/// Works with OpenAI, Groq, Together AI, and any OpenAI-compatible endpoint.
/// </summary>
public sealed class OpenAiLlmClient(
    OpenAiCompatibleClient client,
    IModelRegistry modelRegistry,
    ILogger<OpenAiLlmClient> logger) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(
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

        var inputTokens = 0;
        var outputTokens = 0;
        if (response.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
        }

        logger.LogDebug("LLM response: {Chars} chars, {In}+{Out} tokens",
            text.Length, inputTokens, outputTokens);
        return new LlmResponse(text, inputTokens, outputTokens, assignment.Model);
    }
}
