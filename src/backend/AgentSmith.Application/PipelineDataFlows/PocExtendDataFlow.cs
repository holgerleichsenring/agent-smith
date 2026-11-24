using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-23-c81ee: the poc-extend run — permissive; it reads the server's seeds only.</summary>
public sealed class PocExtendDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.PocExtendName;
}
