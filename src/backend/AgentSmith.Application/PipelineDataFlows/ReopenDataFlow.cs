using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-e061a: the reopen run — permissive, like the filing run; its keys are the server's seeds.</summary>
public sealed class ReopenDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.ReopenName;
}
