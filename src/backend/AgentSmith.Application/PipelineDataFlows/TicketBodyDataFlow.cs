using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>2026-11-17-dd05b: the ticket-body run — permissive, like the other acts; its keys are the server's seeds.</summary>
public sealed class TicketBodyDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.TicketBodyName;
}
