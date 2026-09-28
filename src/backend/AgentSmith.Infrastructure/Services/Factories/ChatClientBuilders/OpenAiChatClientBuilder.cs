using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Models.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for OpenAI, an OpenAI-compatible server (type openai with an
/// endpoint) or Azure OpenAI. All produce IChatClient via Microsoft.Extensions.AI.OpenAI's
/// AsIChatClient extension on the SDK's chat-client object. Azure routes via deployment
/// name, OpenAI by model name.
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
        var endpoint = assignment.EffectiveEndpoint(agent);
        var isAzure = string.Equals(
            assignment.ProviderType ?? agent.Type, "azure_openai", StringComparison.OrdinalIgnoreCase);
        var credential = new ApiKeyCredential(ResolveApiKey(agent, isAzure, endpoint));
        var timeout = ResolveNetworkTimeout(agent);

        if (isAzure)
        {
            var deployment = assignment.Deployment ?? agent.Deployment ?? assignment.Model
                ?? throw new InvalidOperationException(
                    "Azure OpenAI requires a deployment name (per-task or AgentConfig.Deployment).");

            var azureOptions = new AzureOpenAIClientOptions { NetworkTimeout = timeout };
            OwnTheRetries(azureOptions);
            ApplyTestTransport(azureOptions);
            var azure = new AzureOpenAIClient(
                new Uri(endpoint ?? throw new InvalidOperationException("Azure OpenAI requires AgentConfig.Endpoint.")),
                credential, azureOptions);
            return azure.GetChatClient(deployment).AsIChatClient();
        }

        // An endpoint makes this an OpenAI-COMPATIBLE server: the same wire, another host.
        var openAiOptions = new OpenAIClientOptions { NetworkTimeout = timeout };
        if (endpoint is not null) openAiOptions.Endpoint = new Uri(endpoint);
        OwnTheRetries(openAiOptions);
        ApplyTestTransport(openAiOptions);
        var openAi = new OpenAIClient(credential, openAiOptions);
        return openAi.GetChatClient(assignment.Model).AsIChatClient();
    }

    /// <summary>
    /// p0493: the SDK does not retry — TransientRetryChatClient does, one layer up, where the
    /// wait is logged, bounded and re-acquires throttle capacity. Measured through the test
    /// transport below, not assumed: the default policy spent three further attempts on a
    /// 429, honoured Retry-After exactly and with NO ceiling (20s asked → 20s, 20s, 20s), and
    /// waited nothing at all when the header was absent. Stacked under the outer loop that is
    /// up to 24 provider calls for one logical call, in silence, and an hour-long Retry-After
    /// would park the run for three.
    /// </summary>
    private static void OwnTheRetries(ClientPipelineOptions options) =>
        options.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);

    // Point the SDK's client pipeline at the fake handler when a test supplies one.
    private void ApplyTestTransport(ClientPipelineOptions options)
    {
        if (testTransport is not null)
            options.Transport = new HttpClientPipelineTransport(new HttpClient(testTransport));
    }

    /// <summary>
    /// <c>api_key_secret</c> names the environment variable holding the key. With an endpoint
    /// of its own, an OpenAI agent talks to a third-party host, so the key never falls back to
    /// OPENAI_API_KEY there — an OpenAI key is never sent to someone else's server. A named but
    /// empty variable is refused by name; no name at all sends a placeholder, which is what a
    /// local server without authentication accepts.
    /// </summary>
    private static string ResolveApiKey(AgentConfig agent, bool isAzure, string? endpoint)
    {
        var named = string.IsNullOrEmpty(agent.ApiKeySecret)
            ? null : Environment.GetEnvironmentVariable(agent.ApiKeySecret);
        if (!string.IsNullOrEmpty(named)) return named;
        if (!isAzure && endpoint is not null)
            return string.IsNullOrEmpty(agent.ApiKeySecret)
                ? UnauthenticatedKey
                : throw new InvalidOperationException(
                    $"api_key_secret names the environment variable '{agent.ApiKeySecret}', which is empty; "
                    + $"the OpenAI-compatible endpoint {endpoint} gets no other key.");

        return Environment.GetEnvironmentVariable(isAzure ? "AZURE_OPENAI_API_KEY" : "OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "API key (OPENAI_API_KEY / AZURE_OPENAI_API_KEY or configured ApiKeySecret) is required.");
    }

    /// <summary>Sent to an OpenAI-compatible endpoint when no key is named: the SDK needs one.</summary>
    public const string UnauthenticatedKey = "unauthenticated";
}
