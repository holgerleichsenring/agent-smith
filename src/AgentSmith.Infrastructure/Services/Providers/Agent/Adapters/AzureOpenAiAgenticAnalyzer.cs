using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Adapters;

/// <summary>
/// IAgenticAnalyzer for Azure OpenAI. Same shape as OpenAiAgenticAnalyzer but
/// uses AzureOpenAIClient with deployment routing. Composes the OpenAi
/// adapter via a custom ChatClient factory. Optional context compaction (p0114)
/// passes through to the inner OpenAi analyzer.
/// </summary>
public sealed class AzureOpenAiAgenticAnalyzer(
    string apiKey,
    Uri endpoint,
    string deployment,
    RetryConfig retryConfig,
    ILogger<AzureOpenAiAgenticAnalyzer> logger,
    IOpenAiContextCompactor? compactor = null) : IAgenticAnalyzer
{
    private readonly OpenAiAgenticAnalyzer _inner = new(
        apiKey, deployment, endpoint, retryConfig,
        new TypedLogger<OpenAiAgenticAnalyzer>(logger),
        () => CreateAzureChatClient(apiKey, endpoint, deployment, retryConfig, logger),
        compactor);

    public Task<AnalysisResult> AnalyzeAsync(
        string systemPrompt, string userPrompt,
        IReadOnlyList<ToolDefinition> tools, IToolCallHandler handler,
        int maxIterations, CancellationToken cancellationToken) =>
        _inner.AnalyzeAsync(systemPrompt, userPrompt, tools, handler, maxIterations, cancellationToken);

    private static ChatClient CreateAzureChatClient(
        string apiKey, Uri endpoint, string deployment, RetryConfig retryConfig, ILogger logger)
    {
        var http = new ResilientHttpClientFactory(retryConfig, logger).Create();
        var options = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(http)
        };
        var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey), options);
        return client.GetChatClient(deployment);
    }

    /// <summary>Trivial logger forwarder so the inner OpenAi adapter logs through this class's category.</summary>
    private sealed class TypedLogger<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
