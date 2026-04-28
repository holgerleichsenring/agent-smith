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
    ILogger<AzureOpenAiAgentProvider> logger,
    AgentPromptBuilder promptBuilder)
    : OpenAiAgentProvider(apiKey, model, retryConfig, modelRegistry, pricingConfig, logger, promptBuilder)
{
    private const string DefaultApiVersion = "2025-01-01-preview";

    private readonly string _apiKey = apiKey;
    private readonly RetryConfig _retryConfig = retryConfig;
    private readonly string _apiVersion = apiVersion.Length > 0 ? apiVersion : DefaultApiVersion;

    public override string ProviderType => "AzureOpenAI";

    protected override ChatClient CreateChatClient(ModelAssignment assignment)
    {
        var factory = new ResilientHttpClientFactory(_retryConfig, logger);
        var httpClient = factory.Create();

        var options = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient)
        };

        var deploymentName = assignment.Deployment ?? deployment;
        var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(_apiKey), options);
        return client.GetChatClient(deploymentName);
    }
}
