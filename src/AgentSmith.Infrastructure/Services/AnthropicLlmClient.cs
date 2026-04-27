using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Anthropic-backed ILlmClient. Routes to the correct model via IModelRegistry.
/// Reuses a single resilient HttpClient across all calls.
/// </summary>
public sealed class AnthropicLlmClient : ILlmClient, IDisposable
{
    private readonly string _apiKey;
    private readonly IModelRegistry _modelRegistry;
    private readonly ILogger<AnthropicLlmClient> _logger;
    private readonly HttpClient _httpClient;

    public AnthropicLlmClient(
        string apiKey,
        RetryConfig retryConfig,
        IModelRegistry modelRegistry,
        ILogger<AnthropicLlmClient> logger)
    {
        _apiKey = apiKey;
        _modelRegistry = modelRegistry;
        _logger = logger;
        _httpClient = new ResilientHttpClientFactory(retryConfig, logger).Create();
    }

    public Task<LlmResponse> CompleteAsync(
        string systemPrompt, string userPrompt,
        TaskType taskType, CancellationToken cancellationToken) =>
        SendAsync(systemPrompt, BuildSinglePartUserMessage(userPrompt), taskType, cancellationToken);

    public Task<LlmResponse> CompleteWithCachedPrefixAsync(
        string systemPrompt, string userPromptPrefix, string userPromptSuffix,
        TaskType taskType, CancellationToken cancellationToken) =>
        SendAsync(systemPrompt,
            BuildCachedPrefixUserMessage(userPromptPrefix, userPromptSuffix),
            taskType, cancellationToken);

    private async Task<LlmResponse> SendAsync(
        string systemPrompt, Message userMessage,
        TaskType taskType, CancellationToken cancellationToken)
    {
        var model = _modelRegistry.GetModel(taskType);
        _logger.LogDebug("LLM call: task={TaskType}, model={Model}, maxTokens={MaxTokens}",
            taskType, model.Model, model.MaxTokens);

        var client = new AnthropicClient(_apiKey, _httpClient);
        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = model.Model,
                MaxTokens = model.MaxTokens,
                System = new List<SystemMessage> { new(systemPrompt) },
                Messages = new List<Message> { userMessage },
                Stream = false
            },
            cancellationToken);

        return BuildResponse(response, model.Model);
    }

    private LlmResponse BuildResponse(MessageResponse response, string model)
    {
        var text = response.Content.OfType<TextContent>().FirstOrDefault()?.Text?.Trim() ?? "";
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;
        var cacheCreate = response.Usage?.CacheCreationInputTokens ?? 0;
        var cacheRead = response.Usage?.CacheReadInputTokens ?? 0;

        _logger.LogDebug("LLM response: {Chars} chars, {In}+{Out} tokens (cache: {Read} read, {Create} create)",
            text.Length, inputTokens, outputTokens, cacheRead, cacheCreate);
        return new LlmResponse(text, inputTokens, outputTokens, model, cacheCreate, cacheRead);
    }

    private static Message BuildSinglePartUserMessage(string userPrompt) =>
        new()
        {
            Role = RoleType.User,
            Content = new List<ContentBase> { new TextContent { Text = userPrompt } }
        };

    internal static Message BuildCachedPrefixUserMessage(string prefix, string suffix)
    {
        var prefixBlock = new TextContent
        {
            Text = prefix,
            CacheControl = new CacheControl { Type = CacheControlType.ephemeral }
        };
        var content = new List<ContentBase> { prefixBlock };
        if (!string.IsNullOrEmpty(suffix))
            content.Add(new TextContent { Text = suffix });
        return new Message { Role = RoleType.User, Content = content };
    }

    public void Dispose() => _httpClient.Dispose();
}
