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

    // p0270a: ResolveFor moved into the single ConfigResolutionPass
    // (IConfigResolver.ResolveCostCap) so the run path and the dashboard read
    // one resolution with provenance.

    /// <summary>
    /// p0341c: per complexity-tier cost caps. The scope-classification call estimates a
    /// coarse tier (see <see cref="ComplexityTier"/>); ScopeRepos sizes THIS run's
    /// effective cost cap from this map via the existing per-pipeline override slot, so a
    /// bug run gets a small leash and a cross-repo migration a large one. The estimate
    /// only sizes a CEILING — verification is the real judge of done — so a rough tier is
    /// acceptable by design. Absent/failed tier => the static per-pipeline default (fail-safe).
    /// Operator-overridable; the defaults below are the operator's initial call (confirm).
    /// </summary>
    public Dictionary<ComplexityTier, CostCapValues> PerTier { get; init; } = DefaultTierCaps();

    /// <summary>
    /// p0341c: the cap a tier alone would justify — the per-tier override when present,
    /// else the static <see cref="Default"/> (fail-safe on an unmapped tier).
    /// <para>
    /// p0413a: this is the tier's own ceiling, NOT the run's. It knows nothing about
    /// <see cref="PerPipeline"/>, so an estimate applied as a replacement would throw
    /// away the cap the run already resolved. A tier sizes a run's ceiling UP —
    /// <see cref="CostCapValues.RaisedTo"/> is how it is applied.
    /// </para>
    /// </summary>
    public CostCapValues ForTier(ComplexityTier tier) =>
        PerTier.TryGetValue(tier, out var values) && values is not null ? values : Default;

    // p0341c operator-flagged defaults (overridable): trivial ~$1, small/bug ~$2,
    // medium/feature ~$8, large/migration ~$25. Token ceilings scale with the USD leash.
    private static Dictionary<ComplexityTier, CostCapValues> DefaultTierCaps() => new()
    {
        [ComplexityTier.Trivial] = new() { Usd = 1.0m, Tokens = 200_000 },
        [ComplexityTier.Small] = new() { Usd = 2.0m, Tokens = 400_000 },
        [ComplexityTier.Medium] = new() { Usd = 8.0m, Tokens = 1_500_000 },
        [ComplexityTier.Large] = new() { Usd = 25.0m, Tokens = 5_000_000 },
    };
}

/// <summary>
/// p0341c: a coarse, model-estimable complexity bucket for a ticket, returned by the
/// scope-classification call. A model cannot estimate its own effort in calls or dollars
/// (false precision) but a 4-way bucket it can do; the tier only sizes the run's cost
/// CEILING, never gates correctness (verification does), so a rough tier is fine.
/// </summary>
public enum ComplexityTier
{
    /// <summary>Fallback / unknown — sizes to the static per-pipeline default.</summary>
    Unknown = 0,
    Trivial,
    Small,
    Medium,
    Large,
}

/// <summary>
/// USD and token budget for a single pipeline run. Both are checked; either
/// one tripping marks the budget exhausted.
/// </summary>
public sealed class CostCapValues
{
    public decimal Usd { get; init; } = 5.0m;
    public long Tokens { get; init; } = 500_000;

    /// <summary>
    /// p0413a: this cap enlarged to <paramref name="other"/> wherever that is bigger,
    /// per dimension. An ESTIMATE may enlarge the ceiling configuration resolved for a
    /// run; it may never shrink it, because the estimate is a guess and the
    /// configuration is the operator's instruction. Returns this instance unchanged
    /// when the estimate asks for nothing more.
    /// </summary>
    public CostCapValues RaisedTo(CostCapValues other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Usd <= Usd && other.Tokens <= Tokens
            ? this
            : new CostCapValues { Usd = Math.Max(Usd, other.Usd), Tokens = Math.Max(Tokens, other.Tokens) };
    }
}
