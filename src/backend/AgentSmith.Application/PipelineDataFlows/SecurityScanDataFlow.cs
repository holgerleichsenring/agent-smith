namespace AgentSmith.Application.PipelineDataFlows;

public sealed class SecurityScanDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => "security-scan";
}
