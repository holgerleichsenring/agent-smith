using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Models.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for OpenAI or Azure OpenAI. Both produce IChatClient
/// via Microsoft.Extensions.AI.OpenAI's AsIChatClient extension on the SDK's
/// chat-client object. Azure routes via deployment name, OpenAI by model name.
///
/// p0239c: an optional <paramref name="testTransport"/> lets a wire-level test
/// fake the HTTP transport one level below the SDK (the SDK's ClientPipeline is
/// pointed at an HttpClient wrapping the handler), so request shaping + response
/// parsing are observable. Production passes null → the SDK's real transport.
/// </summary>
public sealed class OpenAiChatClientBuilder(HttpMessageHandler? testTransport = null) : IChatClientBuilder
{
    // p0235: the SDK defaults NetworkTimeout to 100s. A large completion exceeds
    // it, the SDK throws TaskCanceledException, and the run dies with a bare "A
    // task was cancelled." Resolve the per-request timeout from config (default 300s).
    public const int DefaultNetworkTimeoutSeconds = 300;

    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "openai", "azure_openai" };

    public static TimeSpan ResolveNetworkTimeout(AgentConfig agent) =>
        TimeSpan.FromSeconds(agent.NetworkTimeoutSeconds > 0
            ? agent.NetworkTimeoutSeconds : DefaultNetworkTimeoutSeconds);

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var apiKey = ResolveApiKey(agent)
            ?? throw new InvalidOperationException(
                "API key (OPENAI_API_KEY / AZURE_OPENAI_API_KEY or configured ApiKeySecret) is required.");

        var credential = new ApiKeyCredential(apiKey);
        var timeout = ResolveNetworkTimeout(agent);

        if (string.Equals(agent.Type, "azure_openai", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = agent.Endpoint
                ?? throw new InvalidOperationException("Azure OpenAI requires AgentConfig.Endpoint.");
            var deployment = assignment.Deployment ?? agent.Deployment ?? assignment.Model
                ?? throw new InvalidOperationException(
                    "Azure OpenAI requires a deployment name (per-task or AgentConfig.Deployment).");

            var azureOptions = new AzureOpenAIClientOptions { NetworkTimeout = timeout };
            ApplyTestTransport(azureOptions);
            var azure = new AzureOpenAIClient(new Uri(endpoint), credential, azureOptions);
            return azure.GetChatClient(deployment).AsIChatClient();
        }

        var openAiOptions = new OpenAIClientOptions { NetworkTimeout = timeout };
        ApplyTestTransport(openAiOptions);
        var openAi = new OpenAIClient(credential, openAiOptions);
        return openAi.GetChatClient(assignment.Model).AsIChatClient();
    }

    // Point the SDK's client pipeline at the fake handler when a test supplies one.
    private void ApplyTestTransport(ClientPipelineOptions options)
    {
        if (testTransport is not null)
            options.Transport = new HttpClientPipelineTransport(new HttpClient(testTransport));
    }

    private static string? ResolveApiKey(AgentConfig agent)
    {
        if (!string.IsNullOrEmpty(agent.ApiKeySecret))
        {
            var secret = Environment.GetEnvironmentVariable(agent.ApiKeySecret);
            if (!string.IsNullOrEmpty(secret)) return secret;
        }

        var fallback = string.Equals(agent.Type, "azure_openai", StringComparison.OrdinalIgnoreCase)
            ? "AZURE_OPENAI_API_KEY"
            : "OPENAI_API_KEY";
        return Environment.GetEnvironmentVariable(fallback);
    }
}
