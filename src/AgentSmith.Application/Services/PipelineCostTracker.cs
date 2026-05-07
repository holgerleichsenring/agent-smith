using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services;

/// <summary>
/// Accumulates LLM token usage and cost across all pipeline steps.
/// Stored in PipelineContext, read by output handlers at the end.
/// Uses pricing from project config; falls back to hardcoded defaults.
/// </summary>
public sealed class PipelineCostTracker
{
    private readonly object _gate = new();
    private int _totalInputTokens;
    private int _totalOutputTokens;
    private int _totalCacheCreateTokens;
    private int _totalCacheReadTokens;
    private int _callCount;
    private string _lastModel = "unknown";
    private readonly Dictionary<string, ModelPricing> _pricing;

    private static readonly Dictionary<string, ModelPricing> DefaultPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-sonnet-4-20250514"] = new() { InputPerMillion = 3.0m, OutputPerMillion = 15.0m, CacheReadPerMillion = 0.30m },
        ["claude-haiku-4-5-20251001"] = new() { InputPerMillion = 0.80m, OutputPerMillion = 4.0m, CacheReadPerMillion = 0.08m },
        ["claude-opus-4-20250514"] = new() { InputPerMillion = 15.0m, OutputPerMillion = 75.0m },
        ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m, CacheReadPerMillion = 0.50m },
        ["gpt-4.1-mini"] = new() { InputPerMillion = 0.40m, OutputPerMillion = 1.60m, CacheReadPerMillion = 0.10m },
        ["gpt-4.1-nano"] = new() { InputPerMillion = 0.10m, OutputPerMillion = 0.40m, CacheReadPerMillion = 0.025m },
        ["gpt-4o"] = new() { InputPerMillion = 2.50m, OutputPerMillion = 10.0m },
        ["gpt-4o-mini"] = new() { InputPerMillion = 0.15m, OutputPerMillion = 0.60m },
        ["llama-3.3-70b-versatile"] = new() { InputPerMillion = 0.0m, OutputPerMillion = 0.0m },
    };

    public PipelineCostTracker(PricingConfig? config = null)
    {
        _pricing = new Dictionary<string, ModelPricing>(DefaultPricing, StringComparer.OrdinalIgnoreCase);
        if (config?.Models is { Count: > 0 })
        {
            foreach (var (model, pricing) in config.Models)
                _pricing[model] = pricing;
        }
    }

    public int TotalInputTokens { get { lock (_gate) return _totalInputTokens; } }
    public int TotalOutputTokens { get { lock (_gate) return _totalOutputTokens; } }
    public int TotalCacheCreateTokens { get { lock (_gate) return _totalCacheCreateTokens; } }
    public int TotalCacheReadTokens { get { lock (_gate) return _totalCacheReadTokens; } }
    public int CallCount { get { lock (_gate) return _callCount; } }

    /// <summary>
    /// Tracks a Microsoft.Extensions.AI ChatResponse. Pulls input/output from
    /// UsageDetails and reads cached/cache-creation counts from AdditionalCounts
    /// (key names vary by provider — Anthropic: cache_read_input_tokens /
    /// cache_creation_input_tokens; OpenAI: cached_tokens).
    /// </summary>
    public void Track(ChatResponse response)
    {
        if (response.Usage is null) return;
        var input = (int)(response.Usage.InputTokenCount ?? 0);
        var output = (int)(response.Usage.OutputTokenCount ?? 0);
        var cacheRead = ReadAdditionalCount(response.Usage, "cache_read_input_tokens")
            + ReadAdditionalCount(response.Usage, "cached_tokens");
        var cacheCreate = ReadAdditionalCount(response.Usage, "cache_creation_input_tokens");
        var billable = Math.Max(0, input - cacheRead);
        var model = response.ModelId;
        lock (_gate)
        {
            _totalInputTokens += billable;
            _totalOutputTokens += output;
            _totalCacheCreateTokens += cacheCreate;
            _totalCacheReadTokens += cacheRead;
            _callCount++;
            if (!string.IsNullOrEmpty(model)) _lastModel = model;
        }
    }

    private static int ReadAdditionalCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? (int)v : 0;

    public decimal EstimateCostUsd()
    {
        lock (_gate) return EstimateCostUsdLocked();
    }

    public override string ToString()
    {
        lock (_gate)
        {
            var cost = EstimateCostUsdLocked();
            var costStr = cost > 0 ? $"${cost:F4}" : "$0.00 (local/free)";
            var cacheStr = _totalCacheReadTokens > 0 || _totalCacheCreateTokens > 0
                ? $" (cache: {_totalCacheReadTokens} read, {_totalCacheCreateTokens} create)"
                : "";
            return $"{_callCount} LLM calls · {_totalInputTokens + _totalOutputTokens} tokens " +
                   $"({_totalInputTokens} in, {_totalOutputTokens} out){cacheStr} · {costStr} · {_lastModel}";
        }
    }

    private decimal EstimateCostUsdLocked()
    {
        var pricing = ResolvePricing(_lastModel);
        if (pricing is null) return 0m;
        return (_totalInputTokens / 1_000_000m * pricing.InputPerMillion) +
               (_totalOutputTokens / 1_000_000m * pricing.OutputPerMillion) +
               (_totalCacheCreateTokens / 1_000_000m * pricing.InputPerMillion * 1.25m) +
               (_totalCacheReadTokens / 1_000_000m * pricing.CacheReadPerMillion);
    }

    // Provider SDKs return date-suffixed ids (gpt-4.1-2025-04-14, claude-sonnet-4-5-20250929)
    // while pricing is registered against the base name (gpt-4.1, claude-sonnet-4-5).
    // Exact lookup first; on miss, longest-prefix wins so gpt-4.1-mini-* beats gpt-4.1.
    private ModelPricing? ResolvePricing(string model)
    {
        if (_pricing.TryGetValue(model, out var exact)) return exact;
        return _pricing
            .Where(kv => model.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Key.Length)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }

    public static PipelineCostTracker GetOrCreate(PipelineContext pipeline)
    {
        const string Key = "PipelineCostTracker";
        if (pipeline.TryGet<PipelineCostTracker>(Key, out var existing)
            && existing is not null)
            return existing;

        pipeline.TryGet<PricingConfig>("ProjectPricing", out var pricingConfig);
        var tracker = new PipelineCostTracker(pricingConfig);
        pipeline.Set(Key, tracker);
        return tracker;
    }
}
