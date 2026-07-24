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

        // p0374: gate the history-cache wire handler on the same signal as the
        // automatic system+tools caching — Cache.IsEnabled=false sends NO directive.
        var promptCaching = CacheTypeResolver.Resolve(agent.Cache);
        var anthropic = BuildClient(apiKey, cacheHistory: promptCaching != PromptCacheType.None);

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
        // so IsEnabled=false sends NO cache directive. p0374 caches the MESSAGE history
        // on top of this via ClaudeHistoryCacheHandler (the adapter marks only
        // system+tools, never messages).
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

    private AnthropicClient BuildClient(string apiKey, bool cacheHistory)
    {
        // Inner transport: the test fake, the OAuth-rewrite handler (sk-ant-oat
        // subscription tokens — p0187), or the default HTTPS handler for a plain
        // sk-ant-api03 key. The SDK still injects the auth header from apiKey either
        // way — the OAuth handler rewrites x-api-key → Bearer downstream.
        HttpMessageHandler inner =
            testTransport
            ?? (apiKey.StartsWith("sk-ant-oat", StringComparison.Ordinal)
                ? new AnthropicOAuthHttpHandler(apiKey, new HttpClientHandler())
                : new HttpClientHandler());

        // p0374: the history-cache handler sits at the TOP of the chain so it edits
        // the fully-shaped request body (system+tools already marked by the adapter)
        // just before it goes to the wire — or the test fake records the patched body.
        var top = cacheHistory ? new ClaudeHistoryCacheHandler(inner) : inner;

        // The default HttpClient.Timeout of 100s applies to the whole SendAsync
        // including the OAuth handler's Retry-After backoff (p0235: the 100s default
        // caused "A task was cancelled"). 10 minutes absorbs several backoff cycles
        // plus a slow Claude response; the SDK does NOT dispose a caller-supplied
        // HttpClient, so holding it for the factory-cached client's lifetime is correct.
        var http = new HttpClient(top) { Timeout = TimeSpan.FromMinutes(10) };
        return new AnthropicClient(apiKey, http);
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
