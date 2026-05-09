namespace AgentSmith.Application.PipelineDataFlows;

/// <summary>fix-bug preset data flow. Permissive baseline; tighten in follow-up phases.</summary>
public sealed class FixBugDataFlow : PermissivePhaseDataFlow
{
    public override string PresetName => "fix-bug";
}
