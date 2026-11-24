using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-23-c81ed: the poc-start run — permissive; it reads the server's seeds only.</summary>
public sealed class PocStartDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.PocStartName;
}
