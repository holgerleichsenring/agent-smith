using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Runs the agentic tool-calling loop against the OpenAI Chat Completions API.
/// Mirrors the Claude AgenticLoop but uses OpenAI SDK types.
/// </summary>
public sealed class OpenAiAgenticLoop(
    ChatClient client,
    ToolExecutor toolExecutor,
    ILogger logger,
    TokenUsageTracker tracker,
    int maxIterations = 25)
{
    public async Task<IReadOnlyList<CodeChange>> RunAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions();
        foreach (var tool in OpenAiToolDefinitions.All)
            options.Tools.Add(tool);

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            logger.LogDebug("OpenAI agentic loop iteration {Iteration}", iteration + 1);

            ChatCompletion completion = await client.CompleteChatAsync(
                messages, options, cancellationToken);

            TrackUsage(completion, iteration + 1);
            messages.Add(new AssistantChatMessage(completion));

            if (completion.FinishReason != ChatFinishReason.ToolCalls)
            {
                logger.LogInformation(
                    "OpenAI agent completed after {Iterations} iterations", iteration + 1);
                break;
            }

            var toolResults = await ProcessToolCallsAsync(completion, cancellationToken);
            foreach (var result in toolResults)
                messages.Add(result);
        }

        tracker.LogSummary(logger);
        return toolExecutor.GetChanges();
    }

    private async Task<List<ToolChatMessage>> ProcessToolCallsAsync(
        ChatCompletion completion, CancellationToken cancellationToken)
    {
        var results = new List<ToolChatMessage>();

        foreach (var toolCall in completion.ToolCalls)
        {
            logger.LogDebug("Executing tool: {Tool}", toolCall.FunctionName);

            JsonNode? input = null;
            if (!string.IsNullOrEmpty(toolCall.FunctionArguments.ToString()))
            {
                input = JsonNode.Parse(toolCall.FunctionArguments.ToString());
            }

            var toolResult = await toolExecutor.ExecuteAsync(toolCall.FunctionName, input);
            results.Add(new ToolChatMessage(toolCall.Id, toolResult));
        }

        return results;
    }

    private void TrackUsage(ChatCompletion completion, int iteration)
    {
        var usage = completion.Usage;
        if (usage is null) return;

        var inputTokens = usage.InputTokenCount;
        var outputTokens = usage.OutputTokenCount;

        tracker.Track(inputTokens, outputTokens);

        logger.LogDebug(
            "OpenAI iteration {Iteration} tokens: Input={Input}, Output={Output}",
            iteration, inputTokens, outputTokens);
    }
}
