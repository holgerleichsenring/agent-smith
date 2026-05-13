namespace AgentSmith.Contracts.Sandbox;

public sealed record SandboxSpec(
    string ToolchainImage,
    ResourceLimits Resources,
    string AgentImage = "agent-smith-sandbox-agent:latest",
    SecretRef? GitTokenSecretRef = null,
    SandboxSecurityContext? SecurityContext = null,
    int TimeoutSeconds = 120);
