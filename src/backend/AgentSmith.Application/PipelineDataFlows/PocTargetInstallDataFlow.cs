using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-23-933dd: the poc-target-install run — permissive; its one key is the server's seed.</summary>
public sealed class PocTargetInstallDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.PocTargetInstallName;
}
