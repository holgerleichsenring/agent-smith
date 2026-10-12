namespace AgentSmith.Application.PipelineDataFlows;

public sealed class ApiSecurityScanDataFlow : PermissiveStepDataFlow
{
    public override string PresetName => "api-security-scan";
}
