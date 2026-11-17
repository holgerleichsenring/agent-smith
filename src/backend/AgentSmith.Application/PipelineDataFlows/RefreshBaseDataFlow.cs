using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-023fb: the refresh-base run reads the server's seeds and the checkout.</summary>
public sealed class RefreshBaseDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.RefreshBaseName;
}
