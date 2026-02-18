using AgentSmith.Contracts.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Calculates run costs from token usage and model pricing configuration.
/// </summary>
public sealed class CostTracker(
    PricingConfig pricing,
    ILogger logger)
{
    /// <summary>
    /// Maps phase names to the model used in that phase.
    /// Must be set before calling CalculateCost.
    /// </summary>
    private readonly Dictionary<string, string> _phaseModels = new();

    public void SetPhaseModel(string phase, string model) =>
        _phaseModels[phase] = model;

    public RunCostSummary CalculateCost(TokenUsageTracker tracker)
    {
        var breakdown = tracker.GetPhaseBreakdown();
        var phases = new Dictionary<string, PhaseCost>();
        var totalCost = 0m;

        foreach (var (phase, usage) in breakdown)
        {
            var model = _phaseModels.GetValueOrDefault(phase, "unknown");
            var cost = CalculatePhaseCost(model, usage);
            phases[phase] = new PhaseCost(
                model, usage.InputTokens, usage.OutputTokens,
                usage.CacheReadTokens, usage.Iterations, cost);
            totalCost += cost;
        }

        return new RunCostSummary(phases.AsReadOnly(), totalCost);
    }

    public void LogCostSummary(RunCostSummary summary)
    {
        logger.LogInformation("Run completed: ${Total:F4} total", summary.TotalCost);

        foreach (var (phase, cost) in summary.Phases)
        {
            logger.LogInformation(
                "  {Phase} ({Model}): ${Cost:F4} ({Input}k input, {Output}k output, {Iter} iter)",
                phase, cost.Model, cost.Cost,
                cost.InputTokens / 1000, cost.OutputTokens / 1000,
                cost.Iterations);
        }
    }

    private decimal CalculatePhaseCost(string model, PhaseUsage usage)
    {
        if (!pricing.Models.TryGetValue(model, out var modelPricing))
        {
            logger.LogWarning("No pricing configured for model {Model}, cost will be 0", model);
            return 0m;
        }

        var inputCost = usage.InputTokens / 1_000_000m * modelPricing.InputPerMillion;
        var outputCost = usage.OutputTokens / 1_000_000m * modelPricing.OutputPerMillion;
        var cacheReadCost = usage.CacheReadTokens / 1_000_000m * modelPricing.CacheReadPerMillion;

        return inputCost + outputCost + cacheReadCost;
    }
}
