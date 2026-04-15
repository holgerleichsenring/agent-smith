using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// Accumulates LLM token usage and cost across all pipeline steps.
/// Stored in PipelineContext, read by output handlers at the end.
/// </summary>
public sealed class PipelineCostTracker
{
    private int _totalInputTokens;
    private int _totalOutputTokens;
    private int _callCount;
    private string _lastModel = "unknown";

    private static readonly Dictionary<string, (decimal InputPerMillion, decimal OutputPerMillion)> KnownPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-sonnet-4-20250514"] = (3.0m, 15.0m),
        ["claude-haiku-4-5-20251001"] = (0.80m, 4.0m),
        ["claude-opus-4-20250514"] = (15.0m, 75.0m),
        ["gpt-4o"] = (2.50m, 10.0m),
        ["gpt-4o-mini"] = (0.15m, 0.60m),
        ["llama-3.3-70b-versatile"] = (0.0m, 0.0m),
    };

    public int TotalInputTokens => _totalInputTokens;
    public int TotalOutputTokens => _totalOutputTokens;
    public int CallCount => _callCount;

    public void Track(LlmResponse response)
    {
        Interlocked.Add(ref _totalInputTokens, response.InputTokens);
        Interlocked.Add(ref _totalOutputTokens, response.OutputTokens);
        Interlocked.Increment(ref _callCount);
        if (response.Model != "unknown") _lastModel = response.Model;
    }

    public decimal EstimateCostUsd()
    {
        if (!KnownPricing.TryGetValue(_lastModel, out var pricing))
            return 0m;

        return (_totalInputTokens / 1_000_000m * pricing.InputPerMillion) +
               (_totalOutputTokens / 1_000_000m * pricing.OutputPerMillion);
    }

    public override string ToString()
    {
        var cost = EstimateCostUsd();
        var costStr = cost > 0 ? $"${cost:F4}" : "$0.00 (no pricing configured)";
        return $"{CallCount} LLM calls · {TotalInputTokens + TotalOutputTokens} tokens " +
               $"({TotalInputTokens} in, {TotalOutputTokens} out) · {costStr} · {_lastModel}";
    }

    public static PipelineCostTracker GetOrCreate(PipelineContext pipeline)
    {
        const string Key = "PipelineCostTracker";
        if (pipeline.TryGet<PipelineCostTracker>(Key, out var existing)
            && existing is not null)
            return existing;

        var tracker = new PipelineCostTracker();
        pipeline.Set(Key, tracker);
        return tracker;
    }
}
