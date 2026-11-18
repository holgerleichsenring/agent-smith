using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-0207a: a series run's beat — permissive, like the filing run; its key is the server's seed.</summary>
public sealed class SeriesRunDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.SeriesRunName;
}
