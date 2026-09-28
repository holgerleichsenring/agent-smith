namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Process-wide run limits, loaded from agentsmith.yml's top-level <c>orchestrator:</c>
/// block. The block once also pinned a spawned orchestrator image; every run now executes
/// in the server, so only the wall-time ceiling is left.
/// </summary>
public sealed class OrchestratorGlobalConfig
{
    /// <summary>
    /// p0200: total pipeline-run wall-time ceiling in seconds. The
    /// PipelineRunWatchdog cancels any active run whose registered
    /// start-time is older than this value. Default 1800 (30 min) is
    /// well above a healthy fix-bug / add-feature run (~5-10 min) and
    /// below the operator-pain threshold (~80 min) seen with stuck runs.
    /// </summary>
    public int MaxRunWallTimeSeconds { get; set; } = 1800;
}
