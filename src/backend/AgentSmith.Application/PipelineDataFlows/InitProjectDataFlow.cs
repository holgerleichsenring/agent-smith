using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.PipelineDataFlows;

public sealed class InitProjectDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => PipelinePresets.InitProjectName;
}
