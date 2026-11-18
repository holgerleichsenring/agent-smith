using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-3435b: the merge-green run — permissive, like the other acts; its key is the server's seed.</summary>
public sealed class MergeGreenDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.MergeGreenName;
}
