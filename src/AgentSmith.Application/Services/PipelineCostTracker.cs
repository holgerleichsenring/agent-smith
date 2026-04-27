using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

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
    public int CallCount { get { lock (_gate) return _callCount; } }

    public void Track(LlmResponse response)
    {
        lock (_gate)
        {
            _totalInputTokens += response.InputTokens;
            _totalOutputTokens += response.OutputTokens;
            _callCount++;
            if (response.Model != "unknown") _lastModel = response.Model;
        }
    }

    public decimal EstimateCostUsd()
    {
        lock (_gate)
        {
            if (!_pricing.TryGetValue(_lastModel, out var pricing))
                return 0m;

            return (_totalInputTokens / 1_000_000m * pricing.InputPerMillion) +
                   (_totalOutputTokens / 1_000_000m * pricing.OutputPerMillion);
        }
    }

    public override string ToString()
    {
        lock (_gate)
        {
            var cost = EstimateCostUsdLocked();
            var costStr = cost > 0 ? $"${cost:F4}" : "$0.00 (local/free)";
            return $"{_callCount} LLM calls · {_totalInputTokens + _totalOutputTokens} tokens " +
                   $"({_totalInputTokens} in, {_totalOutputTokens} out) · {costStr} · {_lastModel}";
        }
    }

    private decimal EstimateCostUsdLocked()
    {
        if (!_pricing.TryGetValue(_lastModel, out var pricing))
            return 0m;
        return (_totalInputTokens / 1_000_000m * pricing.InputPerMillion) +
               (_totalOutputTokens / 1_000_000m * pricing.OutputPerMillion);
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
