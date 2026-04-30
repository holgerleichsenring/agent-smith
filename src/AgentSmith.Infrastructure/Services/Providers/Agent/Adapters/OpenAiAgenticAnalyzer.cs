using System.ClientModel;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Adapters;

/// <summary>
/// IAgenticAnalyzer implementation backed by the OpenAI Chat Completions
/// API. Translates ToolDefinition to ChatTool and back; relies on the SDK's
/// JSON-serialized FunctionArguments / FunctionName / Id round-trip.
/// </summary>
public sealed class OpenAiAgenticAnalyzer(
    string apiKey,
    string model,
    Uri? endpoint,
    RetryConfig retryConfig,
    ILogger<OpenAiAgenticAnalyzer> logger,
    Func<ChatClient>? chatClientFactory = null) : IAgenticAnalyzer
{
    public async Task<AnalysisResult> AnalyzeAsync(
        string systemPrompt, string userPrompt,
        IReadOnlyList<ToolDefinition> tools, IToolCallHandler handler,
        int maxIterations, CancellationToken cancellationToken)
    {
        var client = chatClientFactory is not null ? chatClientFactory() : CreateDefaultClient();
        var options = new ChatCompletionOptions();
        foreach (var tool in tools.Select(ToChatTool)) options.Tools.Add(tool);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var totalIn = 0; var totalOut = 0; var toolCallCount = 0;
        var iteration = 0;
        var finalText = string.Empty;

        for (; iteration < maxIterations; iteration++)
        {
            ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
            totalIn += completion.Usage?.InputTokenCount ?? 0;
            totalOut += completion.Usage?.OutputTokenCount ?? 0;
            messages.Add(new AssistantChatMessage(completion));

            if (completion.FinishReason != ChatFinishReason.ToolCalls)
            {
                finalText = completion.Content.FirstOrDefault()?.Text?.Trim() ?? string.Empty;
                iteration++;
                break;
            }

            foreach (var call in completion.ToolCalls)
            {
                toolCallCount++;
                var input = string.IsNullOrEmpty(call.FunctionArguments.ToString())
                    ? null
                    : JsonNode.Parse(call.FunctionArguments.ToString());
                var result = await handler.HandleAsync(
                    new ToolCall(call.Id, call.FunctionName, input), cancellationToken);
                messages.Add(new ToolChatMessage(result.Id, result.Content));
            }
        }

        logger.LogDebug(
            "OpenAI analyzer: {Iterations} iterations, {ToolCalls} tool calls, {In}+{Out} tokens",
            iteration, toolCallCount, totalIn, totalOut);

        return new AnalysisResult(
            finalText, iteration, toolCallCount,
            new AnalyzerTokenUsage(totalIn, totalOut));
    }

    private static ChatTool ToChatTool(ToolDefinition def) =>
        ChatTool.CreateFunctionTool(def.Name, def.Description,
            BinaryData.FromString(def.InputSchema.ToJsonString()));

    private ChatClient CreateDefaultClient()
    {
        var http = new ResilientHttpClientFactory(retryConfig, logger).Create();
        var options = new OpenAIClientOptions
        {
            Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(http)
        };
        if (endpoint is not null) options.Endpoint = endpoint;
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        return client.GetChatClient(model);
    }
}
