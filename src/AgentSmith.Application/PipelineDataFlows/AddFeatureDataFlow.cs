using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>add-feature preset data flow. Wildcard baseline + p0129a documents the
/// verify-phase data flow with explicit edges; real enforcement requires wildcard
/// removal (post-D7).</summary>
public sealed class AddFeatureDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => "add-feature";

    public override IReadOnlyList<PhaseDataFlowEdge> Edges { get; } =
    [
        Wildcard,
        new("Plan", "Verify", new[] { "PlanJson", "Plan" }),
        new("Implementation", "Verify", new[] { "DiffJson", "CodeChanges" }),
    ];
}
