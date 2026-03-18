using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Runs the agentic tool-calling loop against an Ollama (OpenAI-compatible) endpoint.
/// Uses OpenAiCompatibleClient directly with JSON — no OpenAI SDK dependency.
/// </summary>
public sealed class OllamaAgenticLoop(
    OpenAiCompatibleClient client,
    string model,
    ToolExecutor toolExecutor,
    ILogger logger,
    IProgressReporter progressReporter,
    int maxIterations)
{
    public async Task<IReadOnlyList<CodeChange>> RunAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray
        {
            Msg("system", systemPrompt),
            Msg("user", userMessage)
        };

        var tools = OllamaToolDefinitions.All;

        for (var i = 0; i < maxIterations; i++)
        {
            logger.LogDebug("Ollama agentic loop iteration {Iteration}", i + 1);
            ReportDetail($"\ud83d\udd04 Iteration {i + 1}...", cancellationToken);

            var response = await client.ChatCompleteAsync(
                model, messages, tools, AgentDefaults.DefaultMaxTokens, cancellationToken);

            var choice = response.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            messages.Add(JsonNode.Parse(message.GetRawText()));

            var finishReason = choice.GetProperty("finish_reason").GetString();
            if (finishReason != "tool_calls")
            {
                logger.LogInformation("Ollama agent completed after {Iterations} iterations", i + 1);
                break;
            }

            await ProcessToolCallsAsync(message, messages, cancellationToken);
        }

        return toolExecutor.GetChanges();
    }

    private async Task ProcessToolCallsAsync(
        JsonElement message, JsonArray messages, CancellationToken cancellationToken)
    {
        if (!message.TryGetProperty("tool_calls", out var toolCalls))
            return;

        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            var fn = toolCall.GetProperty("function");
            var name = fn.GetProperty("name").GetString()!;
            var argsStr = fn.GetProperty("arguments").GetRawText();
            var input = JsonNode.Parse(argsStr);
            var callId = toolCall.GetProperty("id").GetString()!;

            logger.LogDebug("Executing tool: {Tool}", name);
            var result = await toolExecutor.ExecuteAsync(name, input);

            messages.Add(new JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = callId,
                ["content"] = result
            });
        }
    }

    private static JsonObject Msg(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private void ReportDetail(string text, CancellationToken ct)
    {
        try { progressReporter?.ReportDetailAsync(text, ct).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }
}
