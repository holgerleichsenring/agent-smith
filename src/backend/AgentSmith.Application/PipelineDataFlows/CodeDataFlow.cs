using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>
/// p0393: data flow for the one code-changing preset. Replaces the fix-bug,
/// fix-no-test, add-feature and phase-execution flows, which carried the same
/// wildcard baseline plus the same two documentary verify-phase edges.
/// </summary>
public sealed class CodeDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => Contracts.Commands.PipelinePresets.CodeName;

    public override IReadOnlyList<PhaseDataFlowEdge> Edges { get; } =
    [
        Wildcard,
        // p0129a: explicit verify-phase edges. Wildcard above still matches first; these
        // document the intended data flow for the post-D7 tightening pass.
        new("Plan", "Verify", new[] { "PlanJson", "Plan" }),
        new("Implementation", "Verify", new[] { "DiffJson", "CodeChanges" }),
    ];
}
