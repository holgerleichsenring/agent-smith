namespace AgentSmith.Contracts.Sandbox;

/// <param name="InitialSourcePath">
/// Optional absolute host-filesystem path to a directory that should be used as the
/// sandbox's <c>/work</c>. Set by the pipeline when a previous step (e.g.
/// TryCheckoutSourceHandler in api-security-scan) cloned the source host-side and the
/// sandbox needs to see the same files at <c>/work</c>. Honored by InProcessSandboxFactory
/// (uses the directory as workDir instead of creating an empty temp dir).
/// Container-based factories (Docker/Kubernetes) ignore this — they have their own
/// in-sandbox source-loading mechanism (CheckoutSourceHandler runs <c>git clone</c>
/// directly into the sandbox's <c>/work</c> volume).
/// </param>
/// <param name="ExtraBinds">
/// Optional extra host-side bind mounts to add to the toolchain container, in
/// docker-style <c>host-path:container-path[:ro]</c> form. Test-only escape hatch
/// (p0199b harness) so a per-test fake git remote (bare repo on the host) can be
/// reached from inside the sandbox at a known path; production pipelines pass an
/// empty list. Honored by DockerContainerSpecBuilder; InProcess and Kubernetes
/// factories ignore it.
/// </param>
public sealed record SandboxSpec(
    string ToolchainImage,
    ResourceLimits Resources,
    string AgentImage = "agent-smith-sandbox-agent:latest",
    SecretRef? GitTokenSecretRef = null,
    SandboxSecurityContext? SecurityContext = null,
    int TimeoutSeconds = 120,
    string? InitialSourcePath = null,
    IReadOnlyList<string>? ExtraBinds = null,
    // p0201: run-id flows into Docker container labels so the orphan reaper
    // can scope cleanup to a single run and the liveness watcher can ask the
    // registry to cancel by run-id. Null/empty leaves the label off (back-
    // compat for callers that build a sandbox outside a pipeline run).
    string? RunId = null);
