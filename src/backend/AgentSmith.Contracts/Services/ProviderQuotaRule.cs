namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: how a provider counts one call against its per-minute token limit.
/// <paramref name="CachedReadWeight"/> is the share of a cache-read token that counts (1 on Azure and OpenAI, whose
/// TPM counts cached prompt tokens; 0.1 on Anthropic, whose ITPM does not count cache reads — the tenth is
/// headroom). <paramref name="CountsMaxOutput"/> says the admission estimate includes the request's max_tokens and
/// keeps it (Azure, OpenAI).
/// </summary>
public sealed record ProviderQuotaRule(double CachedReadWeight, bool CountsMaxOutput)
{
    /// <summary>Every token counts; the output cap does not.</summary>
    public static ProviderQuotaRule EveryToken { get; } = new(CachedReadWeight: 1.0, CountsMaxOutput: false);
}
