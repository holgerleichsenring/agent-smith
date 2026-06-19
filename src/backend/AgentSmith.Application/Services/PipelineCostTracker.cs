using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services;

/// <summary>
/// Accumulates LLM token usage and cost across all pipeline steps.
/// Stored in PipelineContext, read by output handlers at the end.
/// Uses pricing from project config; falls back to hardcoded defaults.
/// p0151d: when constructed with a <see cref="CostCapValues"/>, exposes
/// <see cref="IsBudgetExhausted"/> so the runtime can short-circuit
/// further LLM-driven commands once the per-pipeline cap is reached.
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
    // p0274: project pricing overrides layered over the default resolver via the
    // shared OverlayModelPricingResolver — the SAME merge the live per-call emitter
    // uses (ChatClientFactory), so summary and live cost can't diverge.
    private readonly IModelPricingResolver _pricing;
    private readonly CostCapValues? _costCap;
    private readonly SkillCostScopeManager _scopes = new();

    public PipelineCostTracker(
        IModelPricingResolver? resolver = null,
        PricingConfig? config = null,
        CostCapValues? costCap = null)
    {
        _pricing = new OverlayModelPricingResolver(resolver ?? new ModelPricingResolver(), config);
        _costCap = costCap;
    }

    /// <summary>
    /// p0151d: true once cumulative pipeline cost has crossed either the USD
    /// or token cap configured for this pipeline. <see cref="SkillCallRuntime"/>
    /// checks this on entry and short-circuits the LLM call when set, so
    /// remaining skill rounds are skipped and Compile + Deliver still run.
    /// Returns false when no cap is configured.
    /// </summary>
    public bool IsBudgetExhausted
    {
        get
        {
            if (_costCap is null) return false;
            lock (_gate)
            {
                var totalTokens = (long)_totalInputTokens + _totalOutputTokens
                    + _totalCacheCreateTokens + _totalCacheReadTokens;
                return EstimateCostUsdLocked() > _costCap.Usd
                    || totalTokens > _costCap.Tokens;
            }
        }
    }

    public int TotalInputTokens { get { lock (_gate) return _totalInputTokens; } }
    public int TotalOutputTokens { get { lock (_gate) return _totalOutputTokens; } }
    public int TotalCacheCreateTokens { get { lock (_gate) return _totalCacheCreateTokens; } }
    public int TotalCacheReadTokens { get { lock (_gate) return _totalCacheReadTokens; } }
    public int CallCount { get { lock (_gate) return _callCount; } }

    public IReadOnlyList<CallCostRecord> PerSkillBreakdown => _scopes.PerSkillBreakdown;

    public SkillCallScope BeginCall(
        string skillName, string role, SkillExecutionPhase phase, string? repoName = null)
        => _scopes.BeginCall(skillName, role, phase, this, repoName);

    public void EndCall(SkillCallScope scope, LimitEnforcer? enforcer)
        => _scopes.EndCall(scope, enforcer);

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
        _scopes.AttributeTokens(billable, output, cacheCreate, cacheRead);
    }

    private static int ReadAdditionalCount(UsageDetails usage, string key)
        => usage.AdditionalCounts is { } d && d.TryGetValue(key, out var v) ? (int)v : 0;

    public decimal EstimateCostUsd()
    {
        lock (_gate) return EstimateCostUsdLocked();
    }

    /// <summary>
    /// Snapshots the tracker into a <see cref="RunCostSummary"/> for result.md
    /// frontmatter. Per-skill records are grouped by
    /// <see cref="SkillExecutionPhase"/>; calls that ran outside any explicit
    /// scope (legacy direct-tracker callers like ProjectAnalyzer) land in a
    /// synthetic "Other" phase so totals always reconcile with the tracker's
    /// pipeline-wide counts. Returns null when no LLM calls were tracked, so
    /// callers can decide whether to render the frontmatter cost sections.
    /// </summary>
    public RunCostSummary? BuildSummary()
    {
        lock (_gate)
        {
            if (_callCount == 0) return null;

            var records = _scopes.PerSkillBreakdown;
            var grouped = records
                .GroupBy(r => r.Phase.ToString())
                .ToDictionary(g => g.Key, g => AggregatePhase(g));

            var scopedInput = records.Sum(r => r.InputTokens);
            var scopedOutput = records.Sum(r => r.OutputTokens);
            var unscopedInput = Math.Max(0, _totalInputTokens - (int)scopedInput);
            var unscopedOutput = Math.Max(0, _totalOutputTokens - (int)scopedOutput);
            if (unscopedInput > 0 || unscopedOutput > 0)
            {
                grouped["Other"] = new PhaseCost(
                    Model: _lastModel,
                    InputTokens: unscopedInput,
                    OutputTokens: unscopedOutput,
                    CacheReadTokens: 0,
                    Iterations: Math.Max(0, _callCount - records.Sum(r => r.LlmCallCount)),
                    Cost: EstimateUnscopedCost(unscopedInput, unscopedOutput));
            }

            // p0176a: per-repo split is conditional — only populated when any
            // record carried a RepoName, otherwise legacy single-repo callers
            // get null and result.md skips the per-repo section. Unscoped
            // records (no RepoName) are deliberately dropped from the per-repo
            // view because they can't be attributed; the pipeline total still
            // reflects them via the flat Phases dictionary above.
            IReadOnlyDictionary<string, RepoCost>? perRepo = null;
            var repoBuckets = records
                .Where(r => !string.IsNullOrEmpty(r.RepoName))
                .GroupBy(r => r.RepoName!)
                .ToList();
            if (repoBuckets.Count > 0)
            {
                perRepo = repoBuckets.ToDictionary(
                    bucket => bucket.Key,
                    bucket =>
                    {
                        var phases = bucket
                            .GroupBy(r => r.Phase.ToString())
                            .ToDictionary(g => g.Key, g => AggregatePhase(g));
                        var totalCost = phases.Values.Sum(p => p.Cost);
                        return new RepoCost(phases, totalCost);
                    });
            }

            return new RunCostSummary(grouped, EstimateCostUsdLocked(), perRepo);
        }
    }

    private PhaseCost AggregatePhase(IEnumerable<CallCostRecord> records)
    {
        var input = (int)records.Sum(r => r.InputTokens);
        var output = (int)records.Sum(r => r.OutputTokens);
        var cacheRead = (int)records.Sum(r => r.CacheReadTokens);
        var iterations = records.Sum(r => r.LlmCallCount);
        var pricing = _pricing.Resolve(_lastModel);
        var cost = pricing is null
            ? 0m
            : (input / 1_000_000m * pricing.InputPerMillion)
              + (output / 1_000_000m * pricing.OutputPerMillion)
              + (cacheRead / 1_000_000m * pricing.CacheReadPerMillion);
        return new PhaseCost(_lastModel, input, output, cacheRead, iterations, cost);
    }

    private decimal EstimateUnscopedCost(int input, int output)
    {
        var pricing = _pricing.Resolve(_lastModel);
        if (pricing is null) return 0m;
        return (input / 1_000_000m * pricing.InputPerMillion)
             + (output / 1_000_000m * pricing.OutputPerMillion);
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
        var pricing = _pricing.Resolve(_lastModel);
        if (pricing is null) return 0m;
        return (_totalInputTokens / 1_000_000m * pricing.InputPerMillion) +
               (_totalOutputTokens / 1_000_000m * pricing.OutputPerMillion) +
               (_totalCacheCreateTokens / 1_000_000m * pricing.InputPerMillion * 1.25m) +
               (_totalCacheReadTokens / 1_000_000m * pricing.CacheReadPerMillion);
    }

    public static PipelineCostTracker GetOrCreate(PipelineContext pipeline)
    {
        const string Key = "PipelineCostTracker";
        if (pipeline.TryGet<PipelineCostTracker>(Key, out var existing)
            && existing is not null)
            return existing;

        pipeline.TryGet<IModelPricingResolver>("ModelPricingResolver", out var resolver);
        pipeline.TryGet<PricingConfig>("ProjectPricing", out var pricingConfig);
        pipeline.TryGet<CostCapValues>("PipelineCostCap", out var costCap);
        var tracker = new PipelineCostTracker(resolver, pricingConfig, costCap);
        pipeline.Set(Key, tracker);
        return tracker;
    }
}
