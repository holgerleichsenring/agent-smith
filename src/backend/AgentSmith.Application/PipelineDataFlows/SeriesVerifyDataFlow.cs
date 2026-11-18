using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-0207d: the series check — permissive, like the filing run whose steps it ends with.</summary>
public sealed class SeriesVerifyDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.SeriesVerifyName;
}
