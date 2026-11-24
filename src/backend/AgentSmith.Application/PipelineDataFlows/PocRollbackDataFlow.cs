using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-23-c81ed: the poc-rollback run — permissive; it reads the server's seeds only.</summary>
public sealed class PocRollbackDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.PocRollbackName;
}
