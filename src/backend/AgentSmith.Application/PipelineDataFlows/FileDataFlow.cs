using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-5701b: the filing run — permissive, like spec-dialog; its keys are the server's seeds.</summary>
public sealed class FileDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.FileName;
}
