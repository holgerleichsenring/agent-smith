using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0427: how much per-minute budget a provider/model gets — the operator's override where
/// there is one, the provider's published tier otherwise.
/// <para>
/// Extracted from ChatClientFactory, whose job is resolving a client for a task, not
/// knowing Anthropic's tier numbers. p0188 put the numbers there; they change for reasons
/// that have nothing to do with client construction.
/// </para>
/// </summary>
public static class LlmRateBudget
{
    public static LlmRateLimitOptions For(AgentConfig agent, string providerType)
    {
        var operatorOverride = agent.RateLimit;
        var defaults = DefaultFor(providerType);
        return new LlmRateLimitOptions(
            RequestsPerMinute: operatorOverride?.RequestsPerMinute ?? defaults.RequestsPerMinute,
            InputTokensPerMinute: operatorOverride?.InputTokensPerMinute ?? defaults.InputTokensPerMinute);
    }

    // p0188: a subscription / OAuth token gets a tight budget; the API-key tier defaults to
    // the published Tier 1 numbers. Local / community providers are effectively unlimited.
    private static LlmRateLimitOptions DefaultFor(string providerType) =>
        providerType.ToLowerInvariant() switch
        {
            "claude" or "anthropic" => AnthropicDefault(),
            "openai" or "azure_openai" or "azure-openai" =>
                new LlmRateLimitOptions(RequestsPerMinute: 60, InputTokensPerMinute: 60_000),
            _ => new LlmRateLimitOptions(RequestsPerMinute: 600, InputTokensPerMinute: 600_000),
        };

    private static LlmRateLimitOptions AnthropicDefault() =>
        (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty)
            .StartsWith("sk-ant-oat", StringComparison.Ordinal)
            ? new LlmRateLimitOptions(RequestsPerMinute: 5, InputTokensPerMinute: 20_000)
            : new LlmRateLimitOptions(RequestsPerMinute: 50, InputTokensPerMinute: 40_000);
}
