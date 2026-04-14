using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Azure OpenAI variant of OpenAiAgentProvider.
/// Only overrides client creation to use AzureOpenAIClient with
/// deployment-based URLs and api-key authentication.
/// </summary>
public sealed class AzureOpenAiAgentProvider(
    string apiKey,
    string model,
    string deployment,
    Uri endpoint,
    string apiVersion,
    RetryConfig retryConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<AzureOpenAiAgentProvider> logger)
    : OpenAiAgentProvider(apiKey, model, retryConfig, modelRegistry, pricingConfig, logger)
{
    private const string DefaultApiVersion = "2025-01-01-preview";

    public override string ProviderType => "AzureOpenAI";

    protected override ChatClient CreateChatClient(string modelId)
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();

        var options = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient)
        };

        var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey), options);
        return client.GetChatClient(deployment);
    }
}
