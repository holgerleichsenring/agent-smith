using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>fix-bug preset data flow. Wildcard baseline + p0129a documents the verify-phase
/// data flow with explicit edges; real enforcement requires wildcard removal (post-D7).</summary>
public sealed class FixBugDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => "fix-bug";

    public override IReadOnlyList<PhaseDataFlowEdge> Edges { get; } =
    [
        Wildcard,
        // p0129a: explicit verify-phase edges. Wildcard above still matches first; these
        // document the intended data flow for the post-D7 tightening pass.
        new("Plan", "Verify", new[] { "PlanJson", "Plan" }),
        new("Implementation", "Verify", new[] { "DiffJson", "CodeChanges" }),
    ];
}
