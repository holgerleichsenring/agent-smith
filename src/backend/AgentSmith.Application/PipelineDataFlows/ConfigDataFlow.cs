using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-f3a8c: the config run — permissive; its keys are the server's seeds.</summary>
public sealed class ConfigDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.ConfigName;
}
