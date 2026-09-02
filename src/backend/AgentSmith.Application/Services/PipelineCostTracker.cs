using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Workers;
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
    // p0359: USD accrued call-by-call, each call priced at ITS OWN model. The
    // previous estimate priced ALL accumulated tokens at _lastModel — on a run
    // alternating an expensive and a cheap model (gpt-5.6 + gpt-4o-mini) the
    // whole-run estimate collapsed to the cheap model's rate whenever it ran
    // last, silently under-reporting the budget fence's USD arm.
    private decimal _accruedUsd;
    // p0361: tokens whose model resolved to NO price. Previously these accrued
    // $0 silently — a new model id made runs look cheap or free. Now they are
    // surfaced via UnpricedTokensByModel / ToString / RunCostSummary so the
    // total is an honest lower bound instead of a quiet lie.
    private readonly Dictionary<string, long> _unpricedTokensByModel = new(StringComparer.OrdinalIgnoreCase);
    // p0274: project pricing overrides layered over the default resolver via the
    // shared OverlayModelPricingResolver — the SAME merge the live per-call emitter
    // uses (ChatClientFactory), so summary and live cost can't diverge.
    private readonly IModelPricingResolver _pricing;
    // p0341c: no longer readonly — ScopeRepos sizes the effective cap from the estimated
    // complexity tier AFTER the tracker was first created (its own classification call
    // creates it), so the tier cap is applied in place via ApplyCostCap.
    private CostCapValues? _costCap;
    private readonly SkillCostScopeManager _scopes = new();
    // 2026-09-01-b0d7: worker-CLI spend, in its own channel — see WorkerCallLedger.
    private readonly WorkerCallLedger _workerCalls = new();

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
                return EstimateCostUsdLocked() > _costCap.Usd
                    || EffectiveCapTokensLocked() > _costCap.Tokens;
            }
        }
    }

    // p0376: a cache-read token is ~1/10th the price of a fresh input token
    // ($0.30 vs $3.00 per M on sonnet-5) and is a RE-READ of context already
    // paid for, not new work. Counting cache reads at full weight against the
    // token cap made heavy history-caching (p0374) trip the token arm at a
    // fraction of the USD arm: run c6f2 hit the 15M token cap on 14.6M cache
    // reads at only $6.59 of a $45 USD cap — stopping a FINISHED migration one
    // step before it could emit its verification verdict. Weighting cache reads
    // by the same factor the rate limiter uses makes the token cap track real
    // spend (like the USD arm) instead of cached-context volume. Cache-CREATE
    // stays full weight — it is a genuine write at ~1.25× the input price.
    private const double CacheReadCapWeight = 0.1;

    // 2026-09-01-b0d7: worker volume counts here and NOWHERE in the USD arm. Token volume
    // is real context on any transport, so the token cap binds; no money is spent on that
    // transport, so the money cap has nothing to bind on.
    private long EffectiveCapTokensLocked()
        => Weighted(_totalInputTokens, _totalOutputTokens, _totalCacheCreateTokens,
                _totalCacheReadTokens)
            + Weighted(_workerCalls.InputTokens, _workerCalls.OutputTokens,
                _workerCalls.CacheCreationTokens, _workerCalls.CacheReadTokens);

    private static long Weighted(long input, long output, long cacheCreate, long cacheRead)
        => input + output + cacheCreate + (long)(cacheRead * CacheReadCapWeight);

    /// <summary>p0341c: cumulative tokens across all four buckets — the RAW total, kept
    /// for honest reporting (result.md, metrics). The budget cap uses the cache-weighted
    /// <see cref="EffectiveBudgetTokens"/> instead.</summary>
    public long TotalTokens
    {
        get
        {
            lock (_gate)
                return (long)_totalInputTokens + _totalOutputTokens
                    + _totalCacheCreateTokens + _totalCacheReadTokens;
        }
    }

    /// <summary>
    /// p0376: the cache-weighted token total the per-pipeline token cap compares against
    /// (cache reads discounted — see <see cref="EffectiveCapTokensLocked"/>). The master
    /// loop's own budget fence uses THIS, not raw <see cref="TotalTokens"/>, so its token
    /// check agrees with <see cref="IsBudgetExhausted"/> — the master previously compared
    /// raw TotalTokens and so tripped on 15.16M full-weight tokens (dominated by cache
    /// reads) at a fraction of the USD cap, killing a finished migration before its verdict.
    /// </summary>
    public long EffectiveBudgetTokens
    {
        get { lock (_gate) return EffectiveCapTokensLocked(); }
    }

    /// <summary>
    /// p0341c: size (or resize) the per-pipeline cost cap in place. The scope-classifier
    /// call that estimates the complexity tier ALSO creates this tracker, so the tier cap
    /// must be applied after creation rather than only at construction. Null clears the cap
    /// (fail-open, as before). The already-accumulated spend is preserved.
    /// </summary>
    public void ApplyCostCap(CostCapValues? costCap)
    {
        lock (_gate) _costCap = costCap;
    }

    public int TotalInputTokens { get { lock (_gate) return _totalInputTokens; } }
    public int TotalOutputTokens { get { lock (_gate) return _totalOutputTokens; } }
    public int TotalCacheCreateTokens { get { lock (_gate) return _totalCacheCreateTokens; } }
    public int TotalCacheReadTokens { get { lock (_gate) return _totalCacheReadTokens; } }
    public int CallCount { get { lock (_gate) return _callCount; } }

    /// <summary>p0361: tokens per model for which no price could be resolved.
    /// Empty means every call was priced and the USD total is complete.</summary>
    public IReadOnlyDictionary<string, long> UnpricedTokensByModel
    {
        get { lock (_gate) return new Dictionary<string, long>(_unpricedTokensByModel); }
    }

    public IReadOnlyList<CallCostRecord> PerSkillBreakdown => _scopes.PerSkillBreakdown;

    /// <summary>2026-09-01-b0d7: what the external agent CLI reported, kept apart.</summary>
    public WorkerCallLedger WorkerCalls => _workerCalls;

    public SkillCallScope BeginCall(
        string skillName, string role, SkillExecutionPhase phase, string? repoName = null)
        => _scopes.BeginCall(skillName, role, phase, this, repoName);

    public void EndCall(SkillCallScope scope, LimitEnforcer? enforcer)
        => _scopes.EndCall(scope, enforcer);

    /// <summary>
    /// Tracks a Microsoft.Extensions.AI ChatResponse. Pulls input/output from
    /// UsageDetails and reads cached/cache-creation counts from AdditionalCounts.
    /// p0323: Anthropic.SDK's M.E.AI adapter emits PascalCase keys
    /// (CacheReadInputTokens / CacheCreationInputTokens) — both casings are read.
    /// Anthropic's input_tokens already EXCLUDES cache reads, so only OpenAI's
    /// cached_tokens (a subset of its input total) is subtracted for billable.
    /// </summary>
    public void Track(ChatResponse response)
    {
        if (WorkerCallAccounting.Of(response) is { } worker) { TrackWorkerCall(worker); return; }
        if (response.Usage is null) return;
        var usage = UsageBreakdown.Of(response.Usage);
        var model = response.ModelId;
        var callUsd = 0m;
        var effectiveModel = string.Empty;
        lock (_gate)
        {
            _totalInputTokens += usage.Billable;
            _totalOutputTokens += usage.Output;
            _totalCacheCreateTokens += usage.CacheCreate;
            _totalCacheReadTokens += usage.CacheRead;
            _callCount++;
            effectiveModel = string.IsNullOrEmpty(model) ? _lastModel : model;
            var pricing = _pricing.Resolve(effectiveModel);
            if (pricing is not null)
            {
                callUsd = usage.PriceAt(pricing);
                _accruedUsd += callUsd;
            }
            else
            {
                // p0361: no price for this model — record instead of silently
                // accruing $0, so the summary can flag the total as incomplete.
                _unpricedTokensByModel[effectiveModel] =
                    _unpricedTokensByModel.GetValueOrDefault(effectiveModel) + usage.Total;
            }
            if (!string.IsNullOrEmpty(model)) _lastModel = model;
        }
        _scopes.AttributeTokens(usage.Billable, usage.Output, usage.CacheCreate, usage.CacheRead);
        _scopes.AttributeCost(effectiveModel, callUsd);
    }

    /// <summary>
    /// 2026-09-01-b0d7: the entry point for a call an external agent CLI answered. It is
    /// distinct from <see cref="Track"/> on purpose: the model table would price a
    /// subscription-answered call at API rates, and the unpriced-model path would raise a
    /// pricing alarm for a call that has no price by design. Both produce a wrong number,
    /// so the CLI's own figure is accumulated as the CLI's own figure.
    /// </summary>
    public void TrackWorkerCall(WorkerCallAccounting accounting)
    {
        lock (_gate) _workerCalls.Add(accounting);
    }

    public decimal EstimateCostUsd()
    {
        lock (_gate) return EstimateCostUsdLocked();
    }

    /// <summary>
    /// p0341e: the cost of a SINGLE response's usage at this tracker's pricing, WITHOUT
    /// mutating the running totals or reading the shared counters. Lets a caller report a
    /// per-response cost (e.g. a sub-agent's own per-run cost) consistently with the
    /// pipeline summary, and RACE-FREE under concurrent callers — it reads only the passed
    /// response, never the shared state a before/after Track-delta would straddle. Token
    /// extraction mirrors <see cref="Track"/>; pricing mirrors <see cref="EstimateCostUsdLocked"/>.
    /// </summary>
    public decimal EstimateResponseCostUsd(ChatResponse response)
    {
        // A worker call has no table price by design; pricing it would invent one.
        if (response.Usage is null || WorkerCallAccounting.Of(response) is not null) return 0m;
        var model = string.IsNullOrEmpty(response.ModelId) ? _lastModel : response.ModelId;
        var pricing = _pricing.Resolve(model);
        return pricing is null ? 0m : UsageBreakdown.Of(response.Usage).PriceAt(pricing);
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
            if (_callCount == 0 && _workerCalls.CallCount == 0) return null;

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
                // p0361: the unscoped remainder is priced as the remainder of the
                // per-call accrual, not re-priced at _lastModel — so the phase
                // rows always sum to the headline TotalCost.
                var scopedUsd = records.Sum(r => r.AccruedUsd);
                grouped["Other"] = new PhaseCost(
                    Model: _lastModel,
                    InputTokens: unscopedInput,
                    OutputTokens: unscopedOutput,
                    CacheReadTokens: 0,
                    Cost: Math.Max(0m, _accruedUsd - scopedUsd));
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

            return new RunCostSummary(
                grouped, EstimateCostUsdLocked(), perRepo,
                _unpricedTokensByModel.Count > 0
                    ? new Dictionary<string, long>(_unpricedTokensByModel)
                    : null,
                _workerCalls.Snapshot());
        }
    }

    // p0361: phases carry the per-call accrual (each call priced at its own
    // model) and name the models that actually ran — the previous version
    // re-priced every phase at _lastModel and ignored cache-write cost, so a
    // mixed-model run's phase table disagreed with the headline total.
    private PhaseCost AggregatePhase(IEnumerable<CallCostRecord> records)
    {
        var list = records as IList<CallCostRecord> ?? records.ToList();
        var input = (int)list.Sum(r => r.InputTokens);
        var output = (int)list.Sum(r => r.OutputTokens);
        var cacheRead = (int)list.Sum(r => r.CacheReadTokens);
        var models = list
            .SelectMany(r => r.Model.Split('+', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var model = models.Count > 0 ? string.Join("+", models) : _lastModel;
        return new PhaseCost(model, input, output, cacheRead, list.Sum(r => r.AccruedUsd));
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
            // p0361: never let a missing price read as a cheap run.
            var unpricedStr = _unpricedTokensByModel.Count == 0
                ? ""
                : " · COST INCOMPLETE, no price for: " + string.Join(", ",
                    _unpricedTokensByModel.Select(kv => $"{kv.Key} ({kv.Value} tokens)"));
            var workerStr = _workerCalls.CallCount == 0 ? "" : $" · {_workerCalls}";
            return $"{_callCount} LLM calls · {_totalInputTokens + _totalOutputTokens} tokens " +
                   $"({_totalInputTokens} in, {_totalOutputTokens} out){cacheStr} · {costStr} · {_lastModel}{unpricedStr}{workerStr}";
        }
    }

    // p0359: the run total is the per-call accrual — each call priced at its own
    // model. (The per-phase breakdown in BuildSummary still approximates with
    // _lastModel; only this headline total feeds the budget fence and result.md.)
    private decimal EstimateCostUsdLocked() => _accruedUsd;

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
