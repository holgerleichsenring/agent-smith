using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Adapters;

/// <summary>
/// IAgenticAnalyzer implementation backed by the Anthropic SDK. Translates
/// provider-neutral ToolDefinition / ToolCall / ToolResult records to and
/// from Anthropic's Tool / ToolUseContent / ToolResultContent shape, then
/// runs an iteration loop until the model emits a non-tool-use response.
/// </summary>
public sealed class ClaudeAgenticAnalyzer(
    string apiKey,
    string model,
    int maxTokens,
    RetryConfig retryConfig,
    ILogger<ClaudeAgenticAnalyzer> logger,
    Func<AnthropicClient>? clientFactory = null) : IAgenticAnalyzer
{
    public async Task<AnalysisResult> AnalyzeAsync(
        string systemPrompt, string userPrompt,
        IReadOnlyList<ToolDefinition> tools, IToolCallHandler handler,
        int maxIterations, CancellationToken cancellationToken)
    {
        var client = clientFactory is not null ? clientFactory() : CreateDefaultClient();
        var anthropicTools = tools.Select(ToAnthropicTool).ToList<Anthropic.SDK.Common.Tool>();

        var messages = new List<Message>
        {
            new() { Role = RoleType.User, Content = [new TextContent { Text = userPrompt }] }
        };

        var totalIn = 0; var totalOut = 0; var totalCacheRead = 0; var totalCacheCreate = 0;
        var toolCallCount = 0;
        var iteration = 0;
        var finalText = string.Empty;

        for (; iteration < maxIterations; iteration++)
        {
            var response = await client.Messages.GetClaudeMessageAsync(
                new MessageParameters
                {
                    Model = model,
                    MaxTokens = maxTokens,
                    System = [new SystemMessage(systemPrompt)],
                    Messages = messages,
                    Tools = anthropicTools,
                    Stream = false
                },
                cancellationToken);

            totalIn += response.Usage?.InputTokens ?? 0;
            totalOut += response.Usage?.OutputTokens ?? 0;
            totalCacheRead += response.Usage?.CacheReadInputTokens ?? 0;
            totalCacheCreate += response.Usage?.CacheCreationInputTokens ?? 0;

            messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content });

            var toolUses = response.Content.OfType<ToolUseContent>().ToList();
            if (toolUses.Count == 0)
            {
                finalText = response.Content.OfType<TextContent>()
                    .FirstOrDefault()?.Text?.Trim() ?? string.Empty;
                iteration++; // count this terminal iteration
                break;
            }

            var toolResults = new List<ContentBase>();
            foreach (var use in toolUses)
            {
                toolCallCount++;
                var input = use.Input is null ? null : JsonNode.Parse(use.Input.ToJsonString());
                var result = await handler.HandleAsync(
                    new ToolCall(use.Id, use.Name, input), cancellationToken);
                toolResults.Add(new ToolResultContent
                {
                    ToolUseId = result.Id,
                    Content = [new TextContent { Text = result.Content }],
                    IsError = result.IsError
                });
            }

            messages.Add(new Message { Role = RoleType.User, Content = toolResults });
        }

        logger.LogDebug(
            "Claude analyzer: {Iterations} iterations, {ToolCalls} tool calls, {In}+{Out} tokens",
            iteration, toolCallCount, totalIn, totalOut);

        return new AnalysisResult(
            finalText, iteration, toolCallCount,
            new AnalyzerTokenUsage(totalIn, totalOut, totalCacheRead, totalCacheCreate));
    }

    private AnthropicClient CreateDefaultClient()
    {
        var http = new ResilientHttpClientFactory(retryConfig, logger).Create();
        return new AnthropicClient(apiKey, http);
    }

    private static Anthropic.SDK.Common.Tool ToAnthropicTool(ToolDefinition def) =>
        new(new Function(def.Name, def.Description, JsonNode.Parse(def.InputSchema.ToJsonString())!.AsObject()));
}
