using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-f3a8a: the admin assistant's turn — permissive, like the design turn; its keys are the server's seeds.</summary>
public sealed class AdminTurnDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.AdminTurnName;
}
