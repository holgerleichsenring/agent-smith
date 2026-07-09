using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>p0315d: phase-execution — wildcard baseline like its coding
/// siblings (fix-bug/add-feature); explicit edges come with the post-D7
/// tightening pass.</summary>
public sealed class PhaseExecutionDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => PipelinePresets.PhaseExecutionName;
}
