namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0281c: single deployment-wide image pin (registry + version) that feeds BOTH the
/// orchestrator image and the sandbox-agent image, since both ship from the same
/// agent-smith release. The legacy per-image blocks (sandbox.agent_version /
/// orchestrator.{registry,version}) still load and WIN when set; this is the one-knob
/// base applied to whichever of them the operator left unset.
/// </summary>
public sealed class DeploymentConfig
{
    public string Registry { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}
