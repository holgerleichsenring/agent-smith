namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0151d: per-pipeline cost cap. Bound from the agentsmith.yml
/// <c>pipeline_cost_cap:</c> section. The runtime checks the active
/// PipelineCostTracker against the resolved cap on every Track callback;
/// when the cap is hit, the pipeline step runner skips remaining LLM-driven
/// commands (skill rounds, dispatch, triage) and proceeds straight to
/// Compile + Deliver so the operator still sees partial output.
///
/// Defaults: $5 USD / 500k tokens per pipeline run. Configurable per
/// pipeline via <see cref="PerPipeline"/>.
/// </summary>
public sealed class PipelineCostCapConfig
{
    public CostCapValues Default { get; init; } = new() { Usd = 5.0m, Tokens = 500_000 };
    public Dictionary<string, CostCapValues> PerPipeline { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public CostCapValues ResolveFor(string? pipelineName) =>
        pipelineName is not null && PerPipeline.TryGetValue(pipelineName, out var values)
            ? values
            : Default;
}

/// <summary>
/// USD and token budget for a single pipeline run. Both are checked; either
/// one tripping marks the budget exhausted.
/// </summary>
public sealed class CostCapValues
{
    public decimal Usd { get; init; } = 5.0m;
    public long Tokens { get; init; } = 500_000;
}
