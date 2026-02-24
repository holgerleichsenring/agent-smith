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

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        TaskType taskType,
        CancellationToken cancellationToken)
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
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = userPrompt } }
                    }
                },
                Stream = false
            },
            cancellationToken);

        var text = response.Content.OfType<TextContent>().FirstOrDefault()?.Text?.Trim() ?? "";

        _logger.LogDebug("LLM response: {Chars} chars", text.Length);
        return text;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
