namespace AgentSmith.Application.PipelineDataFlows;

public sealed class ApiSecurityScanDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => "api-security-scan";
}
