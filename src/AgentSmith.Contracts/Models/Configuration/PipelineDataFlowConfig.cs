namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Operator-tunable settings for the p0128c data-flow gating layer. Defaults to
/// warning-only during the D4 cutover so existing pipelines aren't broken on day
/// one; flip <see cref="Enforce"/> to true once the warning log is empty for a
/// given environment's pipeline mix.
/// </summary>
public sealed class PipelineDataFlowConfig
{
    public bool Enforce { get; set; }
}
