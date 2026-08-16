using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Turns one provider response's usage into a dollar figure, and reads the two cache
/// token families the adapters expose differently.
/// <para>
/// p0176b: mirrors PipelineCostTracker.EstimateCostUsdLocked so per-call events agree
/// with the per-pipeline summary. p0323: the two cache families have DIFFERENT input
/// semantics — Anthropic's input_tokens already EXCLUDES cache reads/writes
/// (ExclusiveRead: billed at the cache-read rate, never subtracted), while OpenAI's
/// input total INCLUDES the cached subset (InclusiveRead: subtracted to get the billable
/// portion). cache_create is billed at input rate x 1.25 (Anthropic write penalty).
/// </para>
/// <para>
/// p0423: extracted from <see cref="EventPublishingChatClient"/> — pricing a call and
/// announcing a call are two reasons to change.
/// </para>
/// </summary>
public sealed class LlmCallCostCalculator(IModelPricingResolver pricingResolver)
{
    public decimal ComputeCostUsd(string model, UsageDetails? usage, CacheCounts cache)
    {
        if (usage is null) return 0m;
        var pricing = pricingResolver.Resolve(model);
        if (pricing is null) return 0m;
        var input = (int)(usage.InputTokenCount ?? 0);
        var output = (int)(usage.OutputTokenCount ?? 0);
        var billable = Math.Max(0, input - cache.InclusiveRead);
        var cacheRead = cache.ExclusiveRead + cache.InclusiveRead;
        return (billable / 1_000_000m * pricing.InputPerMillion)
             + (output / 1_000_000m * pricing.OutputPerMillion)
             + (cache.Creation / 1_000_000m * pricing.InputPerMillion * ModelPricing.CacheWritePremium5mTtl)
             + (cacheRead / 1_000_000m * pricing.CacheReadPerMillion);
    }

    /// <summary>
    /// Reads cache token counts off a M.E.AI <see cref="UsageDetails"/>. The two provider
    /// adapters expose them DIFFERENTLY:
    /// <list type="bullet">
    /// <item>Anthropic.SDK 5.10.0 puts cache reads/writes in AdditionalCounts under
    /// PascalCase keys ("CacheReadInputTokens" / "CacheCreationInputTokens"); its
    /// InputTokenCount already EXCLUDES them (-> ExclusiveRead, never subtracted).</item>
    /// <item>M.E.AI.OpenAI 10.3.0 puts the cached prompt subset on the FIRST-CLASS
    /// <c>UsageDetails.CachedInputTokenCount</c> property — it does NOT write
    /// AdditionalCounts["cached_tokens"] at all. Reading that dead key (p0176b/p0323) is
    /// exactly why OpenAI/Azure cache reads always logged 0. Its InputTokenCount INCLUDES
    /// the cached subset (-> InclusiveRead, subtracted to get billable).</item>
    /// </list>
    /// The dead snake_case key is kept only as a forward-compat fallback.
    /// </summary>
    public static CacheCounts ReadCacheCounts(UsageDetails? usage)
    {
        if (usage is null) return default;
        return new CacheCounts(
            ExclusiveRead: ReadAdditionalCount(usage, "CacheReadInputTokens")
                + ReadAdditionalCount(usage, "cache_read_input_tokens"),
            InclusiveRead: usage.CachedInputTokenCount ?? ReadAdditionalCount(usage, "cached_tokens"),
            Creation: ReadAdditionalCount(usage, "CacheCreationInputTokens")
                + ReadAdditionalCount(usage, "cache_creation_input_tokens"));
    }

    private static long ReadAdditionalCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? v : 0;
}
