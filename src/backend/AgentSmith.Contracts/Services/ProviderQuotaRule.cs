namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: how a provider counts one call against its per-minute token limit.
/// <paramref name="CachedReadWeight"/> is the share of a cache-read token that counts (1 on Azure and OpenAI, whose
/// TPM counts cached prompt tokens; 0.1 on Anthropic, whose ITPM does not count cache reads — the tenth is
/// headroom). 2026-11-24-9dc0d: <paramref name="Output"/> says how the output counts — OpenAI's admission keeps the
/// request's max_tokens, Azure counts the tokens actually generated.
/// </summary>
public sealed record ProviderQuotaRule(double CachedReadWeight, OutputCount Output)
{
    /// <summary>Every input token counts; the output does not.</summary>
    public static ProviderQuotaRule EveryToken { get; } = new(CachedReadWeight: 1.0, Output: OutputCount.None);
}
