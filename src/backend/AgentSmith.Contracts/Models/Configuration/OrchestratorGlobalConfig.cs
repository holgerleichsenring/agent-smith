namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Process-wide orchestrator defaults, loaded from agentsmith.yml's top-level
/// <c>orchestrator:</c> block. Per-project <see cref="OrchestratorConfig"/> blocks
/// override these field-by-field.
/// </summary>
public sealed class OrchestratorGlobalConfig
{
    /// <summary>
    /// Container registry the orchestrator image is pulled from
    /// (e.g. <c>ghcr.io/my-org</c>, a private mirror, or empty for an
    /// unprefixed local image reference). Combined with the constant
    /// image-name and <see cref="Version"/> to form the fully-qualified
    /// image reference. Default empty — operators with a published
    /// orchestrator image set this explicitly.
    /// </summary>
    public string Registry { get; set; } = string.Empty;

    /// <summary>
    /// Orchestrator image tag (e.g. <c>0.49.0</c>). No default —
    /// operators MUST set this in agentsmith.yml under either the
    /// top-level <c>orchestrator:</c> block or a per-project
    /// <c>projects.&lt;name&gt;.orchestrator:</c> override. Empty value
    /// triggers a fail-loud at orchestrator-spawn time so a misconfigured
    /// deployment is caught before the first ticket lands instead of
    /// producing an ErrImagePull at the pod level.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// p0200: total pipeline-run wall-time ceiling in seconds. The
    /// PipelineRunWatchdog cancels any active run whose registered
    /// start-time is older than this value. Default 1800 (30 min) is
    /// well above a healthy fix-bug / add-feature run (~5-10 min) and
    /// below the operator-pain threshold (~80 min) seen with stuck runs.
    /// </summary>
    public int MaxRunWallTimeSeconds { get; set; } = 1800;
}
