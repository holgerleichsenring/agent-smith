namespace AgentSmith.Application.PipelineDataFlows;

public sealed class SecurityScanDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => "security-scan";
}
