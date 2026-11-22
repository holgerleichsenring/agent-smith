using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: what a call takes from its deployment on admission — its input estimate scaled by the learned
/// ratio of counted to estimated input, plus its max_tokens where the provider's admission counts it.
/// <see cref="Total"/> is clamped to the bucket (p0350): a call larger than the whole minute waits for a full bucket
/// and proceeds instead of never fitting.
/// </summary>
internal sealed record QuotaReservation(int Input, int Output, int Total)
{
    public static QuotaReservation For(LlmQuotaDemand demand, double ratio, ProviderQuotaRule rule, int capacity)
    {
        var input = (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(1, demand.EstimatedInputTokens) * ratio));
        var output = rule.CountsMaxOutput ? Math.Max(0, demand.MaxOutputTokens) : 0;
        var total = (int)Math.Clamp((long)input + output, 1, Math.Max(1, capacity));
        return new QuotaReservation(input, output, total);
    }
}
