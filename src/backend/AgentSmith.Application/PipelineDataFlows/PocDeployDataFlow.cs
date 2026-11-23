using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-23-933db: the poc-deploy run reads the server's seeds only.</summary>
public sealed class PocDeployDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.PocDeployName;
}
