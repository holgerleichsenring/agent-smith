using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-10-26-bdd0a: the spec derivation — permissive, like spec-dialog.</summary>
public sealed class SpecDeriveDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.SpecDeriveName;
}
