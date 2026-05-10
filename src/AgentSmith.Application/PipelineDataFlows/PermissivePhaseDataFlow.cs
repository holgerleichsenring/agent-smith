using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>
/// Baseline permissive data-flow declaration: a single wildcard edge that
/// allows any prior step to produce any context key for any consumer in the
/// preset. Lets the gating infrastructure stay live with enforce=true without
/// breaking existing handler reads. Per-preset subclasses tighten the edges
/// over time as the data-flow story for each preset solidifies.
/// </summary>
public abstract class PermissivePhaseDataFlow : IPhaseDataFlow
{
    public abstract string PresetName { get; }

    /// <summary>
    /// Wildcard edge that allows any prior step to produce any context key for any
    /// consumer. Subclasses override to add explicit edges alongside the wildcard
    /// (documenting the intended flow); real enforcement requires the wildcard's
    /// removal in a dedicated post-D7 tightening phase.
    /// </summary>
    public virtual IReadOnlyList<PhaseDataFlowEdge> Edges { get; } =
    [
        new("*", "*", new[] { "*" })
    ];

    /// <summary>The wildcard edge subclasses concatenate alongside their explicit edges.</summary>
    protected static readonly PhaseDataFlowEdge Wildcard =
        new("*", "*", new[] { "*" });
}
