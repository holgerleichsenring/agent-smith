namespace AgentSmith.Contracts.Sandbox;

public sealed record SandboxSpec(
    string ToolchainImage,
    string AgentImage = "agent-smith-sandbox-agent:latest",
    SecretRef? GitTokenSecretRef = null,
    ResourceLimits? Resources = null,
    SandboxSecurityContext? SecurityContext = null,
    int TimeoutSeconds = 120);
