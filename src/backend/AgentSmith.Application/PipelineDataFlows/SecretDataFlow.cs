using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-f3a8d: the secret run — permissive; its keys are the server's seeds.</summary>
public sealed class SecretDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.SecretName;
}
