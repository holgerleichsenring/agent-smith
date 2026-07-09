using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>p0315b: spec-dialog conversation turns — permissive, like the
/// other discussion-shaped presets.</summary>
public sealed class SpecDialogDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => PipelinePresets.SpecDialogName;
}
