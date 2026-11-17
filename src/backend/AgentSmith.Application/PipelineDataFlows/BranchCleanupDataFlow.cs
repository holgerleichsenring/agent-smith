using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-713ec: the branch-cleanup preset reads nothing an earlier step publishes.</summary>
public sealed class BranchCleanupDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.BranchCleanupName;
}
