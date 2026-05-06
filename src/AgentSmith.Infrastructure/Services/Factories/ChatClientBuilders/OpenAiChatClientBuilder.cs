using System.ClientModel;
using AgentSmith.Contracts.Models.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for OpenAI or Azure OpenAI. Both produce IChatClient
/// via Microsoft.Extensions.AI.OpenAI's AsIChatClient extension on the SDK's
/// chat-client object. Azure routes via deployment name, OpenAI by model name.
/// </summary>
public sealed class OpenAiChatClientBuilder : IChatClientBuilder
{
    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "openai", "azure-openai" };

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var apiKey = ResolveApiKey(agent)
            ?? throw new InvalidOperationException(
                "API key (OPENAI_API_KEY / AZURE_OPENAI_API_KEY or configured ApiKeySecret) is required.");

        var credential = new ApiKeyCredential(apiKey);

        if (string.Equals(agent.Type, "azure-openai", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = agent.Endpoint
                ?? throw new InvalidOperationException("Azure OpenAI requires AgentConfig.Endpoint.");
            var deployment = assignment.Deployment ?? agent.Deployment ?? assignment.Model
                ?? throw new InvalidOperationException(
                    "Azure OpenAI requires a deployment name (per-task or AgentConfig.Deployment).");

            var azure = new AzureOpenAIClient(new Uri(endpoint), credential);
            return azure.GetChatClient(deployment).AsIChatClient();
        }

        var openAi = new OpenAIClient(credential);
        return openAi.GetChatClient(assignment.Model).AsIChatClient();
    }

    private static string? ResolveApiKey(AgentConfig agent)
    {
        if (!string.IsNullOrEmpty(agent.ApiKeySecret))
        {
            var secret = Environment.GetEnvironmentVariable(agent.ApiKeySecret);
            if (!string.IsNullOrEmpty(secret)) return secret;
        }

        var fallback = string.Equals(agent.Type, "azure-openai", StringComparison.OrdinalIgnoreCase)
            ? "AZURE_OPENAI_API_KEY"
            : "OPENAI_API_KEY";
        return Environment.GetEnvironmentVariable(fallback);
    }
}
