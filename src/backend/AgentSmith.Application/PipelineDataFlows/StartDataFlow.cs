using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-e061b: the start run — permissive, like the filing run; its keys are the server's seeds.</summary>
public sealed class StartDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.StartName;
}
