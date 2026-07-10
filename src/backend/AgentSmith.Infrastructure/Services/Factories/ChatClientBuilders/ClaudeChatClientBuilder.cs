using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// Builds an IChatClient against Anthropic Claude via tghamm/Anthropic.SDK.
/// AnthropicClient.Messages implements IChatClient natively (5.10.0+).
///
/// p0187: when the configured token starts with <c>sk-ant-oat</c> (subscription
/// / OAuth token issued by Claude Code), the SDK's default <c>x-api-key</c>
/// header is rejected by the Anthropic API. We inject a custom HttpClient
/// with a DelegatingHandler that rewrites the auth to
/// <c>Authorization: Bearer</c> for the OAuth path; regular API keys
/// (<c>sk-ant-api03-*</c>) take the SDK default path.
///
/// p0239c: an optional <paramref name="testTransport"/> lets a wire-level test
/// fake the HTTP transport — the SDK is handed an HttpClient wrapping the
/// handler, so request shaping + response parsing are observable. Production
/// passes null → the SDK's default transport (or the OAuth HttpClient).
/// </summary>
public sealed class ClaudeChatClientBuilder(HttpMessageHandler? testTransport = null) : IChatClientBuilder
{
    public IReadOnlyList<string> SupportedTypes { get; } = new[] { "claude", "anthropic" };

    public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
    {
        var apiKey = ResolveApiKey(agent)
            ?? throw new InvalidOperationException(
                $"{AgentEnvKeys.AnthropicApiKey} (or configured ApiKeySecret) is required for type=claude.");

        var anthropic = BuildClient(apiKey);

        // p0187: Anthropic.SDK's IChatClient impl reads model from ChatOptions.ModelId
        // and forwards it as the API's `model` field. Most internal call sites do not
        // set ModelId (Azure OpenAI infers via deployment), so the SDK sends a request
        // without a model → Anthropic 400 "model: Field required". Default ModelId
        // per-call from the resolved assignment so unaware callers keep working.
        var defaultModel = string.IsNullOrEmpty(assignment.Model) ? agent.Model : assignment.Model;
        // p0323: revive the p0007 prompt-caching strategy on the M.E.AI path (it died
        // in p0119a when the last PromptCaching setter was deleted). The SDK's adapter
        // seeds its native MessageParameters from ChatOptions.RawRepresentationFactory
        // (ChatClientHelper.CreateMessageParameters), then SetCacheControls stamps the
        // ephemeral cache_control marker on the last system block + last tool when
        // PromptCaching == AutomaticToolsAndSystem. Resolved once from AgentConfig.Cache
        // so IsEnabled=false sends NO cache directive.
        var promptCaching = CacheTypeResolver.Resolve(agent.Cache);
        return new Microsoft.Extensions.AI.ChatClientBuilder(anthropic.Messages)
            .ConfigureOptions(options =>
            {
                if (string.IsNullOrEmpty(options.ModelId))
                    options.ModelId = defaultModel;
                if (promptCaching != PromptCacheType.None && options.RawRepresentationFactory is null)
                    options.RawRepresentationFactory = _ => new MessageParameters { PromptCaching = promptCaching };
            })
            .Build();
    }

    private AnthropicClient BuildClient(string apiKey)
    {
        if (testTransport is not null)
            return new AnthropicClient(apiKey, new HttpClient(testTransport));
        return apiKey.StartsWith("sk-ant-oat", StringComparison.Ordinal)
            ? new AnthropicClient(apiKey, CreateOAuthHttpClient(apiKey))
            : new AnthropicClient(apiKey);
    }

    private static HttpClient CreateOAuthHttpClient(string token)
    {
        // Anthropic.SDK does NOT dispose a caller-supplied HttpClient (per their
        // README), so leaving the chain alive for the process lifetime is correct
        // — the AnthropicClient instance is constructed per IChatClient resolve
        // and held by the factory cache.
        var handler = new AnthropicOAuthHttpHandler(token, new HttpClientHandler());
        // The default HttpClient.Timeout of 100s applies to the entire SendAsync
        // including our handler's Retry-After backoff. Two 30s backoffs + a 50s
        // response would blow the budget. We manage retries ourselves, so set the
        // ceiling to 10 minutes — long enough to absorb several backoff cycles
        // plus a slow Claude response, short enough that a truly stuck request
        // doesn't hang the pipeline forever.
        return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
    }

    private static string? ResolveApiKey(AgentConfig agent)
    {
        if (!string.IsNullOrEmpty(agent.ApiKeySecret))
        {
            var secret = Environment.GetEnvironmentVariable(agent.ApiKeySecret);
            if (!string.IsNullOrEmpty(secret)) return secret;
        }
        return Environment.GetEnvironmentVariable(AgentEnvKeys.AnthropicApiKey);
    }
}
