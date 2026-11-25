using System.ClientModel;
using System.ClientModel.Primitives;
using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient for OpenAI, an OpenAI-compatible server (type openai with an endpoint) or Azure OpenAI, all
/// via Microsoft.Extensions.AI.OpenAI's AsIChatClient. Azure routes via deployment name, OpenAI by model name.
/// p0239c: an optional <paramref name="testTransport"/> fakes the HTTP transport one level below the SDK, so request
/// shaping and response parsing are observable; production passes null. 2026-11-22-66e4a: with
/// <paramref name="quotaHeaders"/>, every response's remaining-quota headers reach the rate limiter. 2026-11-25-9992k: a
/// role with <c>api: responses</c> takes the Responses API on the same pipeline (<see cref="ResponsesReasoningCall"/>).
/// </summary>
public sealed class OpenAiChatClientBuilder(
    ICredentialResolver credentials, HttpMessageHandler? testTransport = null, ProviderQuotaHeaders? quotaHeaders = null)
    : IChatClientBuilder
{
    // p0235: the SDK defaults NetworkTimeout to 100s. A large completion exceeds
    // it, the SDK throws TaskCanceledException, and the run dies with a bare "A
    // task was cancelled." Resolve the per-request timeout from config (default 300s).
    public const int DefaultNetworkTimeoutSeconds = 300;

    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "openai", "azure_openai" };

    public IReadOnlyList<string> PriceListProviders { get; } = ["openai"];

    public bool AcceptsImageAfterToolResult => true;

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
        if (ModelApi.IsResponses(assignment.Api)) // 2026-11-25-9992k
            return ResponsesReasoningCall.Create(credential, Pipeline(new OpenAIClientOptions { NetworkTimeout = timeout }),
                isAzure, endpoint, isAzure ? assignment.EffectiveDeployment(agent) : assignment.Model, assignment.ReasoningEffort);
        ResponsesReasoningCall.RefuseEffortOnChat(assignment);

        if (isAzure)
        {
            var deployment = assignment.EffectiveDeployment(agent)
                ?? throw new InvalidOperationException(
                    "Azure OpenAI requires a deployment name (per-task or AgentConfig.Deployment).");

            var azure = new AzureOpenAIClient(
                new Uri(endpoint ?? throw new InvalidOperationException("Azure OpenAI requires AgentConfig.Endpoint.")),
                credential, Pipeline(new AzureOpenAIClientOptions { NetworkTimeout = timeout }));
            return azure.GetChatClient(deployment).AsIChatClient();
        }

        // An endpoint makes this an OpenAI-COMPATIBLE server: the same wire, another host.
        var openAiOptions = Pipeline(new OpenAIClientOptions { NetworkTimeout = timeout });
        if (endpoint is not null) openAiOptions.Endpoint = new Uri(endpoint);
        var openAi = new OpenAIClient(credential, openAiOptions);
        return openAi.GetChatClient(assignment.Model).AsIChatClient();
    }

    /// <summary>
    /// The SDK pipeline every client of this builder runs on (2026-11-25-9992k: one method for all three clients).
    /// p0493: the SDK does not retry — TransientRetryChatClient and RateLimitingChatClient do, one layer up, where the
    /// wait is logged and re-acquires throttle capacity. Measured through the test transport: the default policy spent
    /// three further attempts on a 429, honoured Retry-After with no ceiling and waited nothing without the header.
    /// 2026-11-22-66e4a: the quota read sits per try, so with the SDK's retries off it sees exactly the response our layer
    /// sees. A test's fake handler replaces the transport.
    /// </summary>
    private T Pipeline<T>(T options) where T : ClientPipelineOptions
    {
        options.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        if (quotaHeaders is not null) options.AddPolicy(new ProviderQuotaHeaderPolicy(quotaHeaders), PipelinePosition.PerTry);
        if (testTransport is not null) options.Transport = new HttpClientPipelineTransport(new HttpClient(testTransport));
        return options;
    }

    /// <summary>
    /// <c>api_key_secret</c> names the environment variable holding the key, or the catalog secret of that name whose
    /// value may be stored in the admin area (2026-11-18-9ae8). With an endpoint
    /// of its own, an OpenAI agent talks to a third-party host, so the key never falls back to
    /// OPENAI_API_KEY there — an OpenAI key is never sent to someone else's server. A named but
    /// empty variable is refused by name; no name at all sends a placeholder, which is what a
    /// local server without authentication accepts.
    /// </summary>
    private string ResolveApiKey(AgentConfig agent, bool isAzure, string? endpoint)
    {
        var named = new AgentApiKey(credentials).Own(agent);
        if (!string.IsNullOrEmpty(named)) return named;
        if (!isAzure && endpoint is not null)
            return string.IsNullOrEmpty(agent.ApiKeySecret)
                ? UnauthenticatedKey
                : throw new InvalidOperationException(
                    $"api_key_secret names '{agent.ApiKeySecret}', which has no value in the environment or the secret store; "
                    + $"the OpenAI-compatible endpoint {endpoint} gets no other key.");

        return credentials.Named(isAzure ? AgentEnvKeys.AzureOpenAiApiKey : AgentEnvKeys.OpenAiApiKey)
            ?? throw new InvalidOperationException(
                "API key (OPENAI_API_KEY / AZURE_OPENAI_API_KEY or configured ApiKeySecret) is required.");
    }

    /// <summary>Sent to an OpenAI-compatible endpoint when no key is named: the SDK needs one.</summary>
    public const string UnauthenticatedKey = "unauthenticated";
}
