using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: one call's hold on its deployment. Settling replaces the reservation with what the provider
/// counted under its <see cref="ProviderQuotaRule"/> — uncached input, cache writes and the weighted cache reads,
/// plus the max_tokens the admission kept or (2026-11-24-9dc0d) the tokens generated — and caps the deployment at the provider's remaining quota. A refusal
/// owes the call to the refill, pauses the deployment and waits until no refusal pauses it any more.
/// </summary>
internal sealed class LlmQuotaLease(
    DeploymentQuota quota, int estimatedInput, QuotaReservation reserved, TimeProvider clock) : ILlmQuotaLease
{
    private bool _owing;

    public QuotaShortfall? Shortfall { get; private set; }

    public void Settle(UsageDetails? usage, ProviderQuotaReading? reading, TimeSpan callDuration)
    {
        Release();
        var counted = CountedInput(usage, quota.Rule);
        var output = CountedOutput(usage, quota.Rule);
        var budget = quota.TokenCapacity; // 2026-11-22-ab45: the provider's own count says the call cannot fit either
        Shortfall = counted > 0 && counted + output > budget
            ? new((long)counted + output, (long)counted, output, budget) { IsGenerated = quota.Rule.Output == OutputCount.Generated } : null;
        // The learned ratio stays input-only: it scales the next admission's input estimate.
        if (counted > 0) quota.Debit(counted + output - reserved.Total, counted, Math.Max(1, estimatedInput));
        if (reading is not null) quota.Align(reading, callDuration);
    }

    public async Task RefusedAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        quota.Refuse(reserved.Total, first: !_owing, wait);
        _owing = true;
        // A sibling refused later may have paused the deployment past this call's own interval.
        for (var rest = quota.Paused(); rest > TimeSpan.Zero; rest = quota.Paused())
            await Task.Delay(rest, clock, cancellationToken);
    }

    public void Release()
    {
        if (!_owing) return;
        _owing = false;
        quota.Release(reserved.Total);
    }

    /// <summary>2026-11-24-9dc0d: the output the provider counts — the max_tokens the admission kept, or what was generated.</summary>
    private int CountedOutput(UsageDetails? usage, ProviderQuotaRule rule) => rule.Output switch
    {
        OutputCount.MaxTokens => reserved.Output,
        OutputCount.Generated => (int)Math.Min(int.MaxValue, Math.Max(0, usage?.OutputTokenCount ?? 0)),
        _ => 0,
    };

    /// <summary>The input tokens the provider counts toward its limit. Azure and OpenAI report cached tokens INSIDE
    /// the input count; Anthropic reports cache reads and writes beside it.</summary>
    internal static double CountedInput(UsageDetails? usage, ProviderQuotaRule rule)
    {
        if (usage is null) return 0;
        var cache = LlmCallCostCalculator.ReadCacheCounts(usage);
        var uncached = Math.Max(0, (usage.InputTokenCount ?? 0) - cache.InclusiveRead);
        var cachedReads = cache.InclusiveRead + cache.ExclusiveRead;
        return uncached + cache.Creation + (rule.CachedReadWeight * cachedReads);
    }
}
