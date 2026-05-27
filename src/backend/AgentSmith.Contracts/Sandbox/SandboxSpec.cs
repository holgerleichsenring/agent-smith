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
public sealed record SandboxSpec(
    string ToolchainImage,
    ResourceLimits Resources,
    string AgentImage = "agent-smith-sandbox-agent:latest",
    SecretRef? GitTokenSecretRef = null,
    SandboxSecurityContext? SecurityContext = null,
    int TimeoutSeconds = 120,
    string? InitialSourcePath = null);
