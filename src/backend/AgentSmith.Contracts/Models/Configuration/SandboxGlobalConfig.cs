namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Process-wide sandbox defaults, loaded from agentsmith.yml's top-level
/// <c>sandbox:</c> block. Per-project <see cref="SandboxConfig"/> blocks
/// override these field-by-field.
/// </summary>
public sealed class SandboxGlobalConfig
{
    /// <summary>
    /// Container registry the sandbox agent image is pulled from
    /// (e.g. <c>holgerleichsenring</c>, <c>ghcr.io/my-org</c>, or a private
    /// mirror). Combined with the constant image-name and <see cref="AgentVersion"/>
    /// to form the fully-qualified image reference.
    /// </summary>
    public string AgentRegistry { get; set; } = AgentSmith.Contracts.Constants.AgentImageDefaults.DefaultRegistry;

    /// <summary>
    /// Sandbox agent image tag (e.g. <c>0.48.0</c>). No default — operators MUST set this
    /// in agentsmith.yml under either the top-level <c>sandbox:</c> block or a
    /// per-project <c>projects.&lt;name&gt;.sandbox:</c> override. Empty value triggers a
    /// fail-loud at sandbox-spawn time so a misconfigured deployment is caught
    /// before the first ticket lands instead of producing an ErrImagePull at the
    /// pod level.
    /// </summary>
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>
    /// p0200: per-sandbox-step wall-time cap in seconds. Caps any incoming
    /// <c>Step.TimeoutSeconds</c> before the container backend computes its
    /// channel-wait (channel-wait stays cap + 30s grace).
    ///
    /// Default 900 (15 min) accommodates real-world C# / Node test suites:
    /// Sample's integration tests need ~5-10 min for restore + build +
    /// run inside a clean DockerSandbox; the prior 300s (TestHandler) /
    /// 120s (initial p0200 draft) defaults wedged the operator's first
    /// successful registry-auth run mid-test on 2026-06-02.
    /// Operators tuning for fast-failure on micro-services lower this in
    /// agentsmith.yml's top-level <c>sandbox:</c> block.
    /// </summary>
    public int StepTimeoutSeconds { get; set; } = 900;
}
