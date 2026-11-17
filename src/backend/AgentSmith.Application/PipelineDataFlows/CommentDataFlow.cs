using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-e061c: the comment run — permissive, like the filing run; its keys are the server's seeds.</summary>
public sealed class CommentDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.CommentName;
}
