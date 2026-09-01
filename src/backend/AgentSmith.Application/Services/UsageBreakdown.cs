using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0323/p0376, extracted 2026-09-01-b0d7: the four token buckets a response's usage
/// actually carries, read once and identically wherever they are needed.
/// <para>
/// Three producers write the same facts under different names — Anthropic's M.E.AI
/// adapter uses PascalCase AdditionalCounts, the worker bridge writes the snake_case
/// names, and OpenAI/Azure report their cached subset on the first-class
/// <see cref="UsageDetails.CachedInputTokenCount"/>. Reading only some of them is why
/// OpenAI cache reads priced as zero for months, and two call sites reading them
/// separately is how they drift apart again.
/// </para>
/// </summary>
internal sealed record UsageBreakdown(int Billable, int Output, int CacheCreate, int CacheRead)
{
    public static UsageBreakdown Of(UsageDetails usage)
    {
        // Only OpenAI's cached subset is subtracted: it is part of ITS input total, while
        // Anthropic's input_tokens already excludes what was read from cache.
        var openAiCached = (int)(usage.CachedInputTokenCount ?? Count(usage, "cached_tokens"));
        return new UsageBreakdown(
            Math.Max(0, (int)(usage.InputTokenCount ?? 0) - openAiCached),
            (int)(usage.OutputTokenCount ?? 0),
            Count(usage, "CacheCreationInputTokens") + Count(usage, "cache_creation_input_tokens"),
            Count(usage, "CacheReadInputTokens") + Count(usage, "cache_read_input_tokens")
                + openAiCached);
    }

    public long Total => (long)Billable + Output + CacheCreate + CacheRead;

    public decimal PriceAt(ModelPricing pricing) =>
        (Billable / 1_000_000m * pricing.InputPerMillion)
        + (Output / 1_000_000m * pricing.OutputPerMillion)
        + (CacheCreate / 1_000_000m * pricing.InputPerMillion * ModelPricing.CacheWritePremium5mTtl)
        + (CacheRead / 1_000_000m * pricing.CacheReadPerMillion);

    private static int Count(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } counts && counts.TryGetValue(key, out var value)
            ? (int)value : 0;
}
