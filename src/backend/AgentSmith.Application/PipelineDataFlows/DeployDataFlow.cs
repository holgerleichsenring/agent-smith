using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-18-3863c: the deploy run reads the server's seeds only.</summary>
public sealed class DeployDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.DeployName;
}
