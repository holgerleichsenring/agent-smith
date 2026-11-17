using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-9a39c: the retarget run — permissive, like the other acts; its keys are the server's seeds.</summary>
public sealed class RetargetDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.RetargetName;
}
