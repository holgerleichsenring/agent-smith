using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-23-933dg: the poc-variables run — permissive; its one key is the server's in-memory seed.</summary>
public sealed class PocVariablesDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.PocVariablesName;
}
