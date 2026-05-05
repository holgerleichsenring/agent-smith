using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Cost;

/// <summary>
/// Base implementation of <see cref="ICostTracker"/>. Wraps a <see cref="TokenUsageTracker"/>
/// for token aggregation; provider-specific derived classes are responsible for extracting
/// canonical billable/cached/output tokens from their native API response types and feeding
/// them into <see cref="Aggregate"/>. The cost math is centralized here.
/// </summary>
public abstract class CostTrackerBase : ICostTracker
{
    private readonly PricingConfig _pricing;
    private readonly TokenUsageTracker _tokenTracker;
    private readonly Dictionary<string, string> _phaseModels = new();

    protected CostTrackerBase(PricingConfig pricing, ILogger logger, TokenUsageTracker? tokenTracker = null)
    {
        _pricing = pricing;
        Logger = logger;
        _tokenTracker = tokenTracker ?? new TokenUsageTracker();
    }

    protected ILogger Logger { get; }

    /// <summary>
    /// The underlying token tracker. Exposed so that paths that don't have a typed response
    /// (e.g. compactor summarizer calls) can still record raw int counts via existing APIs.
    /// </summary>
    public TokenUsageTracker TokenTracker => _tokenTracker;

    public void SetPhase(string phase) => _tokenTracker.SetPhase(phase);

    public void SetPhaseModel(string phase, string model) => _phaseModels[phase] = model;

    /// <summary>
    /// Records a single API response in canonical form. Derived classes call this from
    /// their provider-typed Track methods after extracting the relevant fields.
    /// </summary>
    /// <param name="billableInput">Input tokens billed at the full input price (excludes cached portions).</param>
    /// <param name="output">Output/completion tokens.</param>
    /// <param name="cacheCreate">Tokens written to cache.</param>
    /// <param name="cacheRead">Tokens read from cache (priced at the cache-read rate).</param>
    protected void Aggregate(int billableInput, int output, int cacheCreate = 0, int cacheRead = 0)
        => _tokenTracker.Track(billableInput, output, cacheCreate, cacheRead);

    public RunCostSummary CalculateCost()
    {
        var phases = new Dictionary<string, PhaseCost>();
        var totalCost = 0m;

        foreach (var (phase, usage) in _tokenTracker.GetPhaseBreakdown())
        {
            var model = _phaseModels.GetValueOrDefault(phase, "unknown");
            var cost = ComputePhaseCost(model, usage);
            phases[phase] = new PhaseCost(
                model,
                usage.InputTokens,
                usage.OutputTokens,
                usage.CacheReadTokens,
                usage.Iterations,
                cost);
            totalCost += cost;
        }

        return new RunCostSummary(phases.AsReadOnly(), totalCost);
    }

    public TokenUsageSummary GetTokenSummary() => _tokenTracker.GetSummary();

    public IReadOnlyDictionary<string, PhaseUsage> GetPhaseBreakdown() => _tokenTracker.GetPhaseBreakdown();

    public void LogCostSummary(ILogger logger)
    {
        var summary = CalculateCost();
        logger.LogInformation("Run completed: ${Total:F4} total", summary.TotalCost);

        foreach (var (phase, cost) in summary.Phases)
        {
            logger.LogInformation(
                "  {Phase} ({Model}): ${Cost:F4} ({Input}k input, {Output}k output, {CacheRead}k cache-read, {Iter} iter)",
                phase, cost.Model, cost.Cost,
                cost.InputTokens / 1000, cost.OutputTokens / 1000,
                cost.CacheReadTokens / 1000,
                cost.Iterations);
        }
    }

    public void LogTokenSummary(ILogger logger) => _tokenTracker.LogSummary(logger);

    private decimal ComputePhaseCost(string model, PhaseUsage usage)
    {
        if (!_pricing.Models.TryGetValue(model, out var modelPricing))
        {
            Logger.LogWarning("No pricing configured for model {Model}, cost will be 0", model);
            return 0m;
        }

        var inputCost = usage.InputTokens / 1_000_000m * modelPricing.InputPerMillion;
        var outputCost = usage.OutputTokens / 1_000_000m * modelPricing.OutputPerMillion;
        var cacheReadCost = usage.CacheReadTokens / 1_000_000m * modelPricing.CacheReadPerMillion;

        return inputCost + outputCost + cacheReadCost;
    }
}
